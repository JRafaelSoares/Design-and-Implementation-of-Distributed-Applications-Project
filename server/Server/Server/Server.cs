using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
namespace MSDAD
{
    namespace Server
    {
        class Server : MarshalByRefObject, IMSDADServer, IMSDADServerPuppet, IMSDADServerToServer
        {
            private readonly ConcurrentDictionary<String, Meeting> Meetings = new ConcurrentDictionary<string, Meeting>();

            private readonly String SeverId;

            private readonly uint MaxFaults;

            private readonly int MinDelay;

            private readonly int MaxDelay;

            private readonly static WorkQueue workQueue = new WorkQueue();

            private static readonly Object CreateMeetingLock = new object();

            private static readonly Random random = new Random();

            //private static int ticket = 0;
            //private static int currentTicket = 0;

            public List<IMSDADServerToServer> ServerURLs { get; } = new List<IMSDADServerToServer>();
            public HashSet<ServerClient> ClientURLs { get; } = new HashSet<ServerClient>();

            public Server(String ServerId, uint MaxFaults, int MinDelay, int MaxDelay)
            {
                this.SeverId = ServerId;
                this.MaxFaults = MaxFaults;
                this.MinDelay = MinDelay;
                this.MaxDelay = MaxDelay;

            }

            static void Main(string[] args)
            {

                if (args.Length < 9)
                {
                    Console.WriteLine("<Usage> Server server_ip server_id network_name port max_faults min_delay max_delay num_servers server_urls numLocations locations");
                    System.Console.WriteLine(" Press < enter > to shutdown server...");
                    System.Console.ReadLine();
                    return;
                }

                //Initialize Server
                TcpChannel channel = new TcpChannel(Int32.Parse(args[3]));
                ChannelServices.RegisterChannel(channel, false);
                Server server = new Server(args[1], UInt32.Parse(args[4]), Int32.Parse(args[5]), Int32.Parse(args[6]));
                RemotingServices.Marshal(server, args[2], typeof(Server));

                //Get Server URLS and connect to them
                List<Dictionary<String, Meeting>> allMeetings = new List<Dictionary<String, Meeting>>();
                int i;
                for (i = 8; i < 8 + Int32.Parse(args[7]); ++i)
                {
                    System.Console.WriteLine(args[i]);
                    IMSDADServerToServer otherServer = (IMSDADServerToServer)Activator.GetObject(typeof(IMSDADServer), args[i]);
                    if (otherServer != null)
                    {
                        server.ServerURLs.Add(otherServer);
                        ServerState state = otherServer.RegisterNewServer("tcp://" + args[0] + ":" + args[3] + "/" + args[2]);
                        server.AddUsers(state.Clients);
                        allMeetings.Add(state.Meetings);
                    }
                    else
                    {
                        System.Console.WriteLine("Cannot connect to server at address {0}", args[i]);
                    }

                }

                //Create Locations
                int j = i + 1;
                for (i = j; i < j + 3 * Int32.Parse(args[j - 1]); i += 3)
                {
                    ((IMSDADServerPuppet)server).AddRoom(args[i], UInt32.Parse(args[i + 1]), args[i + 2]);
                }

                server.CalculateMeetingState(allMeetings);

                System.Console.WriteLine(String.Format("ip: {0} ServerId: {1} network_name: {2} port: {3} max faults: {4} min delay: {5} max delay: {6}", args[0], args[1], args[2], args[3], args[4], args[5], args[6]));
                System.Console.WriteLine(" Press < enter > to shutdown server...");
                System.Console.ReadLine();
                }

            void CalculateMeetingState(List<Dictionary<String, Meeting>> meetings)
            {
                lock (Meetings)
                {
                    foreach (Dictionary<String, Meeting> meetingDict in meetings)
                    {
                        foreach (Meeting meeting in meetingDict.Values)
                        {
                            if (!this.Meetings.ContainsKey(meeting.Topic))
                            {
                                this.Meetings.TryAdd(meeting.Topic, meeting);
                            }
                            else if (this.Meetings[meeting.Topic].CurState == Meeting.State.Open && meeting.CurState != Meeting.State.Open)
                            {
                                this.Meetings[meeting.Topic] = meeting;
                            }
                        }
                    }
                }

            }
            //Leases never expire
            public override object InitializeLifetimeService()
            {
                return null;
            }

            /*void ServerCreateMeeting (string coordId, string topic, uint minParticipants, List<string> slots, HashSet<string> invitees)
            {
                /*int myTicket = Interlocked.Increment(ref ticket) -1 ;
                while (myTicket != currentTicket)
                {
                    System.Threading.Thread.Sleep(250);
                }
                this.ServerCreateMeeting(coordId, topic, minParticipants, slots, invitees);
                Interlocked.Increment(ref currentTicket);* / 

                workQueue.AddWork(delegate ()
                {
                    this.ServerCreateMeeting(coordId, topic, minParticipants, slots, invitees);

                });

            }*/

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
                SafeSleep();
                lock (CreateMeetingLock)
                {
                    Meetings.TryAdd(topic, meeting);
                }

                foreach (IMSDADServerToServer server in ServerURLs)
                {
                    server.CreateMeeting(topic, meeting);
                }
                if (meeting.GetType() == typeof(MeetingInvitees))
                {
                    MeetingInvitees invRef = (MeetingInvitees)meeting;
                    HashSet<ServerClient> invitees = new HashSet<ServerClient>();
                    foreach (ServerClient client in ClientURLs) {
                        if (invRef.Invitees.Contains(client.ClientId))
                        {
                            invitees.Add(client);
                        }
                    }
                    return invitees;
                }
                else
                {
                    return ClientURLs;
                }
            }


            void IMSDADServer.JoinMeeting(String topic, List<String> slots, String userId, DateTime timestamp)
            {
                SafeSleep();
                bool found = Meetings.TryGetValue(topic, out Meeting meeting);

                if (!found)
                {
                    throw new NoSuchMeetingException("Meeting specified does not exist on this server");
                }
                if (meeting.CurState != Meeting.State.Open)
                {
                    throw new CannotJoinMeetingException("Meeting is no longer open");
                }

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
            }

            Dictionary<String, Meeting> IMSDADServer.ListMeetings(Dictionary<String, Meeting> meetings)
            {

                SafeSleep();
                foreach (String key in meetings.Keys.ToList())
                {
                    bool found = this.Meetings.TryGetValue(key, out Meeting myMeeting);

                    if (!found)
                    {

                        Meetings.TryAdd(key, meetings[key]);
                    }
                    else
                    {
                        lock (Meetings.Keys.FirstOrDefault(k => k.Equals(key)))
                        {
                            Meeting upToDate = myMeeting.MergeMeeting(meetings[key]);
                            this.Meetings[key] = upToDate;
                            meetings[key] = upToDate;
                        }
                    }
                }

                return meetings;
            }

            void IMSDADServer.CloseMeeting(String topic, String userId)
            {
                SafeSleep();

                bool found = Meetings.TryGetValue(topic, out Meeting meeting);

                if ((!found) || meeting.CurState != Meeting.State.Open)
                {
                    throw new TopicDoesNotExistException("Topic " + topic + " cannot be closed\n");
                }
                //Lock other threads from closing any other Meetings
                lock (this.Meetings)
                {
                    //Lock other threads from joining this meeting
                    lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                    {
                        if (meeting.CoordenatorID != userId)
                        {
                            throw new ClientNotCoordenatorException("Client " + userId + " is not this topic Coordenator.\n");
                        }


                        List<Slot> slots = meeting.Slots.Where(x => x.GetNumUsers() >= meeting.MinParticipants).ToList();


                        if (slots.Count == 0)
                        {
                            meeting.CurState = Meeting.State.Canceled;
                            throw new NoMeetingAvailableException("No slot meets the requirements. Meeting Canceled\n");
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
                }
            }

            void IMSDADServerPuppet.AddRoom(String location, uint capacity, String roomName)
            {
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
                        String id = server.Ping();
                        Console.WriteLine(String.Format("Server {0} is alive", id));
                    }
                    catch (RemotingException)
                    {
                        Console.WriteLine("Could not contact Server");
                    }
                }
            }

            String IMSDADServerToServer.Ping()
            {
                return this.SeverId;

            }

            void IMSDADServer.NewClient(string url, string id)
            {

                ((IMSDADServerToServer)this).RegisterNewClient(url, id);

                foreach (IMSDADServerToServer server in this.ServerURLs)
                {
                    server.RegisterNewClient(url, id);
                }
            }

            void AddUsers(HashSet<ServerClient> clients)
            {
                lock (this.ClientURLs)
                {
                    foreach (ServerClient client in clients)
                    {
                        this.ClientURLs.Add(client);
                    }
                }
            }

            ServerState IMSDADServerToServer.RegisterNewServer(string url)
            {
                Dictionary<String, Meeting> newDictionary;
                //Impossible for a server to Register while a Meeting is being closed 
                //To ensure that a new server always gets the meeting closed if it is being closed when it joins
                lock (this.Meetings)
                {
                    IMSDADServerToServer otherServer = (IMSDADServerToServer)Activator.GetObject(typeof(IMSDADServer), url);
                    if (otherServer != null)
                    {
                        ServerURLs.Add(otherServer);
                    }
                    else
                    {
                        System.Console.WriteLine("Cannot connect to server at address {0}", url);
                    }


                    newDictionary = this.Meetings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                return new ServerState(ClientURLs, newDictionary);
            }

            void IMSDADServerToServer.RegisterNewClient(string url, string id)
            {
                lock (ClientURLs)
                {
                    this.ClientURLs.Add(new ServerClient(url, id));
                }
            }

            void IMSDADServerPuppet.ShutDown()
            {
                Environment.Exit(1);
            }

            void IMSDADServerToServer.CreateMeeting(String topic, Meeting meeting)
            {
                lock (CreateMeetingLock)
                {
                    if (!Meetings.TryAdd(topic, meeting))
                    {
                        Meetings[topic] = Meetings[topic].MergeMeeting(meeting);
                    }


                }
            }

        }
    }
}
