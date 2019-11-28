using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.Remoting.Messaging;
using System.Reflection;

namespace MSDAD
{
    namespace Server
    {
        class Server : MarshalByRefObject, IMSDADServer, IMSDADServerPuppet, IMSDADServerToServer
        {
            private readonly ConcurrentDictionary<String, Meeting> Meetings = new ConcurrentDictionary<string, Meeting>();

            private readonly String SeverId;
            private readonly String ServerUrl;
            private readonly uint MaxFaults;
            private readonly int MinDelay;
            private readonly int MaxDelay;

            private bool LeaderToken { get; set; }

            private static readonly Object CloseMeetingLock = new object();

            private static readonly Random random = new Random();

            public delegate ConcurrentDictionary<String, Meeting> ListAsyncDelegate();

            public delegate Meeting RemoteAsyncDelegate(String topic);

            public delegate Meeting JoinAsyncDelegate(String topic, List<string> slots, String userId, DateTime timestamp);
            
            public delegate void MergeMeetingDelegate(String topic, Meeting meeting);

            /*Properties for Reliable Broadcast*/
            public delegate void RBSendDelegate(String messageId, String operation, object[] args);
            public ConcurrentDictionary<String, CountdownEvent> RBMessages = new ConcurrentDictionary<string, CountdownEvent>();
            public int RBMessageCounter = 0;

            public List<IMSDADServerToServer> ServerURLs { get; } = new List<IMSDADServerToServer>();
            public ConcurrentDictionary<ServerClient, byte> ClientURLs { get; } = new ConcurrentDictionary<ServerClient, byte>();

            public Server(String ServerId, uint MaxFaults, int MinDelay, int MaxDelay, String ServerUrl)
            {
                this.SeverId = ServerId;
                this.MaxFaults = MaxFaults;
                this.MinDelay = MinDelay;
                this.MaxDelay = MaxDelay;
                this.ServerUrl = ServerUrl;

            }

            static void Main(string[] args)
            {

                if (args.Length < 9)
                {
                    Console.WriteLine("<Usage> Server server_ip server_id network_name port max_faults min_delay max_delay num_servers server_urls numLocations locations");
                    Console.WriteLine(" Press < enter > to shutdown server...");
                    Console.ReadLine();
                    return;
                }
                String ServerUrl = "tcp://" + args[0] + ":" + args[3] + "/" + args[2];
                
                Console.WriteLine(String.Format("Server with id {0} Initializing  with url {1}", args[1], ServerUrl));

                //Initialize Server
                TcpChannel channel = new TcpChannel(Int32.Parse(args[3]));
                ChannelServices.RegisterChannel(channel, false);
                Server server = new Server(args[1], UInt32.Parse(args[4]), Int32.Parse(args[5]), Int32.Parse(args[6]), ServerUrl);
                RemotingServices.Marshal(server, args[2], typeof(Server));

                //Get Server URLS and connect to them
                int i;
                for (i = 8; i < 8 + Int32.Parse(args[7]); ++i)
                {
                    Console.WriteLine("Connecting to server with url {0}", args[i]);

                    IMSDADServerToServer otherServer = (IMSDADServerToServer)Activator.GetObject(typeof(IMSDADServer), args[i]);
                    if (otherServer != null)
                    {
                        //FIXME Review: we don't need Server State since all the servers are created on the beginning
                        // of the system and none crashes while the system is setup
                        Console.WriteLine(String.Format("Successfully connected to server with url {0} successfully", args[i]));
                        server.ServerURLs.Add(otherServer);
                        otherServer.NewServer(server.ServerUrl);
                    }
                    else
                    {
                        //Should never happen
                        Console.WriteLine(String.Format("Could not connect to server at address {0}", args[i]));
                    }

                }

                //Means that it is the first server to be created
                if (server.ServerURLs.Count == 0)
                {
                    server.LeaderToken = true;
                }

                //Create Locations
                int j = i + 1;
                for (i = j; i < j + 3 * Int32.Parse(args[j - 1]); i += 3)
                {
                    Console.WriteLine(String.Format("Adding room: {0} {1} {2}", args[i], args[i + 1], args[i + 2]));
                    ((IMSDADServerPuppet)server).AddRoom(args[i], UInt32.Parse(args[i + 1]), args[i + 2]);
                }

                
                Console.WriteLine(String.Format("Setup successfull!\nip: {0} ServerId: {1} network_name: {2} port: {3} max faults: {4} min delay: {5} max delay: {6}", args[0], args[1], args[2], args[3], args[4], args[5], args[6]));
                Console.WriteLine(" Press < enter > to shutdown server...");
                Console.ReadLine();
            }

            //Client Leases never expire
            public override object InitializeLifetimeService()
            {
                return null;
            }

            
            private void SafeSleep()
            {
                int mili = random.Next(MinDelay, MaxDelay);
                if (mili != 0)
                {
                    Thread.Sleep(mili);
                }
                return;
            }

            HashSet<ServerClient> IMSDADServer.CreateMeeting(string topic, Meeting meeting)
            {
                Console.WriteLine(String.Format("Broadcast meeting with topic {0} to other servers", topic));
                //FIXME We should make it causally ordered
                ((IMSDADServerToServer)this).RB_Send(RBNextMessageId(), "CreateMeeting", new object[] { topic, meeting });
                Console.WriteLine(String.Format("meeting with topic {0} broadcasted successfully", topic));

                return this.GetMeetingInvitees(this.Meetings[topic]);
            }

            HashSet<ServerClient> GetMeetingInvitees(Meeting meeting)
            {
                //Give Client URLS of other clients that can join
                //FIXME Should create meeting difusion algorithm here
                HashSet<ServerClient> clients = new HashSet<ServerClient>();
                foreach (ServerClient client in ClientURLs.Keys)
                {
                    if (meeting.CanJoin(client.ClientId))
                    {
                        clients.Add(client);
                    }
                }
                return clients;
            }


            Meeting IMSDADServerToServer.JoinMeeting(String topic, List<String> slots, String userId, DateTime timestamp)
            {
                SafeSleep();

                bool found = Meetings.TryGetValue(topic, out Meeting meeting);

                //FIXME Join will be causal with create and as such this will never happen
                if (!found)
                {
                    throw new NoSuchMeetingException("Meeting specified does not exist on this server");
                }

                //See if Meeting as reached the server yet
                if (meeting.CurState != Meeting.State.Open)
                {
                    //FIXME Maybe just return since this is called by another server?
                    throw new CannotJoinMeetingException("Meeting is no longer open");
                }

                //Join Client to Meeting
                lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                {

                    if (!meeting.CanJoin(userId))
                    {
                        throw new CannotJoinMeetingException("User " + userId + " cannot join this meeting.\n");
                    }

                    List<Slot> givenSlots = Slot.ParseSlots(slots);
                    foreach (Slot slot in meeting.Slots.Where(x => givenSlots.Contains(x)))
                    {

                        slot.AddUserId(userId, timestamp);

                    }
                    meeting.AddUser(userId, timestamp);
                }

                return meeting;
            }


            Meeting IMSDADServer.JoinMeeting(string topic, List<string> slots, string userId, DateTime timestamp)
            {
                SafeSleep();
                Console.WriteLine(String.Format("User {0} wants to join meeting with topic {1}", userId, topic));
                bool found = Meetings.TryGetValue(topic, out Meeting meeting);

                //Meeting hasn't reached the server yet
                if (!found)
                {
                    Console.WriteLine(String.Format("meeting with topic {0} hasn't reached this server yet, so user {1} must retry later", topic, userId));
                    throw new NoSuchMeetingException("Meeting specified does not exist on this server");
                }

                CountdownEvent latch = new CountdownEvent((int)this.MaxFaults);
                ((IMSDADServerToServer)this).JoinMeeting(topic, slots, userId, timestamp);

                
                Console.WriteLine(String.Format("propagate Join of user {0} to meeting with topic {1} to {2} servers ", userId, topic, this.MaxFaults));

                //Propagate the join and wait for maxFaults responses
                //FIXME Should be causal send Join Meeting
                foreach (IMSDADServerToServer otherServer in this.ServerURLs)
                {
                    JoinAsyncDelegate RemoteDel = new JoinAsyncDelegate(otherServer.JoinMeeting);
                    AsyncCallback RemoteCallback = new AsyncCallback(ar =>
                    {
                        {
                            JoinAsyncDelegate del = (JoinAsyncDelegate)((AsyncResult)ar).AsyncDelegate;
                            del.EndInvoke(ar);
                            latch.Signal();
                        }
                    });
                    IAsyncResult RemAr = RemoteDel.BeginInvoke(topic, slots, userId, timestamp, RemoteCallback, null);
                }
                latch.Wait();
                latch.Dispose();

                Console.WriteLine(String.Format("User {0} joined meeting with topic {1} finished", userId, topic));

                return Meetings[topic];
            }

            IDictionary<String, Meeting> IMSDADServer.ListMeetings(Dictionary<String, Meeting> meetings)
            {
                //FIXME Should ask f servers for the state of the meetings given.
                SafeSleep();
                ListMeetingsMerge(meetings);

                CountdownEvent latch = new CountdownEvent((int)this.MaxFaults);

                foreach (IMSDADServerToServer otherServer in this.ServerURLs)
                {
                    ListAsyncDelegate RemoteDel = new ListAsyncDelegate(otherServer.getMeetings);
                    AsyncCallback RemoteCallback = new AsyncCallback(ar =>
                    {
                        {
                            ListAsyncDelegate del = (ListAsyncDelegate)((AsyncResult)ar).AsyncDelegate;
                            ConcurrentDictionary<String, Meeting> serverMeeting = del.EndInvoke(ar);
                            ListMeetingsMerge(serverMeeting);
                            latch.Signal();
                        }
                    });
                    IAsyncResult RemAr = RemoteDel.BeginInvoke(RemoteCallback, null);
                }
                latch.Wait();
                latch.Dispose();
                
                return this.Meetings;
            }

            

            void IMSDADServerToServer.CloseMeeting(String topic, Meeting meeting)
            {
                //Lock other threads from closing any Meetings
                lock (CloseMeetingLock)
                {
                    Meetings[topic] = meeting;
                    //Lock other threads from joining or updating this meeting
                    lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                    {

                        List<Slot> slots = meeting.Slots.Where(x => x.GetNumUsers() >= meeting.MinParticipants).ToList();

                        if (slots.Count == 0)
                        {
                            meeting.CurState = Meeting.State.Canceled;
                            PropagateClosedMeeting(topic, meeting);
                            return;
                        }

                        slots.Sort((x, y) =>
                        {
                            return (int)(y.GetNumUsers() - x.GetNumUsers());
                        });

                        uint numUsers = slots[0].GetNumUsers();
                        DateTime date = slots[0].Date;

                        //Only those with maximum potential users
                        slots = slots.Where(x => x.GetNumUsers() == numUsers).ToList();

                        //Tightest room
                        slots.Sort((x, y) =>
                        {
                            return (int)(x.Location.GetBestFittingRoomForCapacity(date, numUsers).Capacity - y.Location.GetBestFittingRoomForCapacity(date, numUsers).Capacity);
                        });

                        meeting.Close(slots[0], numUsers);
                    }
                    PropagateClosedMeeting(topic, meeting);
                }
            }

            void PropagateClosedMeeting(String topic, Meeting meeting)
            {
                Object objLock = new Object();
                CountdownEvent latch = new CountdownEvent(this.ServerURLs.Count);

                //Propagate the closed meeting to all the servers and await for all responses
                foreach (IMSDADServerToServer otherServer in this.ServerURLs)
                {
                    MergeMeetingDelegate RemoteDel = new MergeMeetingDelegate(otherServer.MergeClosedMeeting);
                    AsyncCallback RemoteCallback = new AsyncCallback(ar =>
                    {
                        lock (objLock)
                        {
                            MergeMeetingDelegate del = (MergeMeetingDelegate)((AsyncResult)ar).AsyncDelegate;
                            del.EndInvoke(ar);
                            latch.Signal();
                        }
                    });
                    IAsyncResult RemAr = RemoteDel.BeginInvoke(topic, meeting, RemoteCallback, null);
                }
                latch.Wait();
                latch.Dispose();

            }
            void IMSDADServerToServer.MergeClosedMeeting(string topic, Meeting meeting)
            {
                lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                {
                    //Update Meeting and book room if Meeting was closed
                    Meetings[topic] = meeting;
                    meeting.BookClosedMeeting();
                }
            }
            void IMSDADServerPuppet.AddRoom(String location, uint capacity, String roomName)
            {
                //Block Other threads from Adding Rooms as well
                lock (Location.Locations)
                {
                    Location local = Location.FromName(location);
                    if (local == null)
                    {
                        local = new Location(location);
                        Location.AddLocation(local);
                    }
                    local.AddRoom(new Room(roomName, capacity));
                }
            }
            void IMSDADServerPuppet.Crash()
            {
                Environment.Exit(1);
            }
            void IMSDADServerPuppet.Freeze()
            {
                throw new NotImplementedException();
            }
            void IMSDADServerPuppet.Unfreeze()
            {
                throw new NotImplementedException();
            }
            void IMSDADServerPuppet.Status()
            {
                foreach (IMSDADServerToServer server in ServerURLs)
                {
                    try
                    {
                        String ping = server.Ping();
                        Console.WriteLine(ping);
                    }
                    catch (RemotingException)
                    {
                        Console.WriteLine("Could not contact Server");
                    }
                }
            }

            String IMSDADServerToServer.Ping()
            {
                SafeSleep();
                return String.Format("Server with id {1} is alive at url {0}", this.ServerUrl, this.SeverId);
            }

            void IMSDADServer.NewClient(string url, string id)
            {
                Console.WriteLine(String.Format("Broadcast new client: id {0} url {1}", id, url));
                
                ((IMSDADServerToServer)this).RB_Send(RBNextMessageId(), "NewClient", new object[]{ url, id });
                
                Console.WriteLine(String.Format("New client connected: id {0} url {1}", id, url));
            }

            void IMSDADServerToServer.NewClient(string url, string id)
            {
                Console.WriteLine(String.Format("Client with id {0} and url {1} reached server {2}", id, url, this.SeverId));
                this.ClientURLs.TryAdd(new ServerClient(url, id), new byte());
                
            }

            void IMSDADServerToServer.NewServer(string url)
            {
                Console.WriteLine("Trying to Connect to server at address {0}", url);
                IMSDADServerToServer otherServer = (IMSDADServerToServer)Activator.GetObject(typeof(IMSDADServer), url);
                    if (otherServer != null)
                    {
                        ServerURLs.Add(otherServer);
                        Console.WriteLine("Successfully connected to server at address {0}", url);
                    }
                    else
                    {
                        Console.WriteLine("Cannot connect to server at address {0}", url);
                    }
            }
            
            void IMSDADServerPuppet.ShutDown()
            {
                Environment.Exit(1);
            }

            void IMSDADServerToServer.CreateMeeting(String topic, Meeting meeting)
            {
                Console.WriteLine(String.Format("Meeting with topic {0} reached server with id", topic, this.SeverId));
                Meetings.TryAdd(topic, meeting);
            }

            Meeting IMSDADServerToServer.LockMeeting(string topic)
            {
                //Lock meeting as fase one of closing a meeting
                lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                {
                    this.Meetings[topic].CurState = Meeting.State.Pending;
                    return Meetings[topic];
                }
            }

            //FIXME Must change this
            void IMSDADServer.CloseMeeting(string topic, string userId)
            {
                Console.WriteLine(String.Format("Client with id {0} wants to close meeting with topic {1}", userId, topic));
                lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                {
                    SafeSleep();
                    Object objLock = new Object();
                    CountdownEvent latch = new CountdownEvent(this.ServerURLs.Count);
                    //Lock users from joining local meeting
                    this.Meetings[topic].CurState = Meeting.State.Pending;

                    //Lock users from joining a meeting on the other servers
                    foreach (IMSDADServerToServer otherServer in this.ServerURLs)
                    {
                        RemoteAsyncDelegate RemoteDel = new RemoteAsyncDelegate(otherServer.LockMeeting);
                        AsyncCallback RemoteCallback = new AsyncCallback(ar =>
                        {
                            lock (objLock)
                            {
                                RemoteAsyncDelegate del = (RemoteAsyncDelegate)((AsyncResult)ar).AsyncDelegate;
                                this.Meetings[topic] = this.Meetings[topic].MergeMeeting(del.EndInvoke(ar));
                                latch.Signal();
                            }
                        });
                        IAsyncResult RemAr = RemoteDel.BeginInvoke(topic, RemoteCallback, null);
                    }
                    latch.Wait();
                    latch.Dispose();

                    //Leader can now close Meeting
                    if (LeaderToken)
                    {
                        ((IMSDADServerToServer)this).CloseMeeting(topic, this.Meetings[topic]);
                    }
                    else
                    {
                        IMSDADServerToServer leader = this.ServerURLs[0];
                        MergeMeetingDelegate del = new MergeMeetingDelegate(leader.CloseMeeting);
                        del.BeginInvoke(topic, this.Meetings[topic], null, null);
                    }
                }
            }

            /***********************************************************************************************************************/ 
            /**************************************Reliable Broadcast***************************************************************/
            /***********************************************************************************************************************/
             
            private String RBNextMessageId()
            {
                return String.Format("{0}-{1}", this.SeverId, Interlocked.Increment(ref this.RBMessageCounter));
            }

            /// <summary>
            /// Reliably Broadcast a Method, meaning that certainly if the method is executed then 
            /// it will certainly be executed on all servers that are correct(i.e that do not crash)
            /// Algorithm: If it is the first time this message is seen on this server then broadcast it to all and wait for f acknowledges
            /// where f is the number of faults. When f servers received the message means that the message will never be lost on the system
            /// </summary>
            /// <param name="messageId"> unique id that identifies every message sent on the server </param>
            /// <param name="operation"> the method to run on the server</param>
            /// <param name="args"> arguments for the method to be ran</param>
            /// FIXME: No way to get return type
            /// FIXME Perguntar se Max Faults nunca vai ser zero
            void IMSDADServerToServer.RB_Send(string messageId, string operation, object[] args)
            {
                if (RBMessages.TryAdd(messageId, new CountdownEvent((int)MaxFaults)))
                {
                    //First time seeing this message, rebroadcast and wait for F acks
                    foreach (IMSDADServerToServer server in this.ServerURLs)
                    {
                        RBSendDelegate remoteDel = new RBSendDelegate(server.RB_Send);
                        IAsyncResult RemAr = remoteDel.BeginInvoke(messageId, operation, args, null, null);
                    }
                    RBMessages[messageId].Wait();
                    GetType().GetInterface("IMSDADServerToServer").GetMethod(operation).Invoke(this, args);
                }
                else
                {
                    //Already seen this message, ack
                    lock (RBMessages[messageId])
                    {
                        if (!RBMessages[messageId].IsSet)
                        {
                            RBMessages[messageId].Signal();
                        }
                    }
                }
            }

            ConcurrentDictionary<String, Meeting> IMSDADServerToServer.getMeetings()
            {
                return Meetings;
            }

            //Aux function to merge lists of meetingsmeetings
            void ListMeetingsMerge(IDictionary<String, Meeting> meetings)
            {
                foreach (String key in meetings.Keys.ToList())
                {
                    bool found = this.Meetings.TryGetValue(key, out Meeting myMeeting);

                    //If a client has a meeting I don't know about get that meeting
                    if (!found)
                    {
                        Meetings.TryAdd(key, meetings[key]);
                    }
                    else
                    {
                        //Merge meeting on the server and give the merged meeting to the client as well

                        lock (Meetings.Keys.FirstOrDefault(k => k.Equals(key)))
                        {
                            Meeting upToDate = myMeeting.MergeMeeting(meetings[key]);
                            this.Meetings[key] = upToDate;
                            //TODO Is this okay if its a list from a server
                            meetings[key] = upToDate;
                        }
                    }
                }
            }
        }
    }
}

