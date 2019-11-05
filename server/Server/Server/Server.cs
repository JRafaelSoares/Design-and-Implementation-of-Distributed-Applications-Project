using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Linq;
using System.Threading;
using System.Net;

namespace MSDAD
{
    namespace Server
    {
        class Server : MarshalByRefObject, IMSDADServer, IMSDADServerPuppet, IMSDADServerToServer
        {
            private readonly Dictionary<String, Meeting> Meetings = new Dictionary<string, Meeting>();

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
                if(args.Length < 9)
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
                int i;
                for (i = 8; i < 8 + Int32.Parse(args[7]); ++i)
                {
                    IMSDADServerToServer otherServer = (IMSDADServerToServer)Activator.GetObject(typeof(IMSDADServer), args[i]);
                    if (otherServer != null)
                    {
                        server.ServerURLs.Add(otherServer);
                        otherServer.RegisterNewServer("tcp://" + args[0] + ":" + args[3] + "/" + args[2]);   
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

                System.Console.WriteLine(String.Format("ip: {0} ServerId: {1} network_name: {2} port: {3} max faults: {4} min delay: {5} max delay: {6}", args[0], args[1], args[2], args[3], args[4], args[5], args[6]));
                System.Console.WriteLine(" Press < enter > to shutdown server...");
                System.Console.ReadLine();
            }

            //Leases never expire
            public override object InitializeLifetimeService()
            {
                return null;
            }

            void ServerCreateMeeting (string coordId, string topic, uint minParticipants, List<string> slots, HashSet<string> invitees)
            {
                /*int myTicket = Interlocked.Increment(ref ticket) -1 ;
                while (myTicket != currentTicket)
                {
                    System.Threading.Thread.Sleep(250);
                }
                this.ServerCreateMeeting(coordId, topic, minParticipants, slots, invitees);
                Interlocked.Increment(ref currentTicket);*/

                workQueue.AddWork(delegate ()
                {
                    this.ServerCreateMeeting(coordId, topic, minParticipants, slots, invitees);

                });


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

            void IMSDADServer.CreateMeeting(string coordId, string topic, uint minParticipants, List<string> slots, HashSet<string> invitees)
            {
                SafeSleep();
                lock(CreateMeetingLock) {
                    bool found = Meetings.TryGetValue(topic, out Meeting meeting);
                    if (found)
                    {
                        throw new CannotCreateMeetingException("A meeting with that topic already exists");
                    }
                    if (invitees == null)
                    {
                        Meetings.Add(topic, new Meeting(coordId, topic, minParticipants, slots));
                    }
                    else
                    {
                        Meetings.Add(topic, new MeetingInvitees(coordId, topic, minParticipants, slots, invitees));
                    }
                }
            }


            void IMSDADServer.JoinMeeting(String topic, List<String> slots, String userId, DateTime timestamp)
            {
                SafeSleep();
                bool found = Meetings.TryGetValue(topic, out Meeting meeting);

                if (!found || meeting.CurState != Meeting.State.Open)
                {
                    throw new NoSuchMeetingException("Meeting specified does not exist on this server");
                }

                lock (meeting)
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

            String IMSDADServer.ListMeetings(String userId)
            {
                SafeSleep();
                Thread.Sleep(random.Next(MinDelay, MaxDelay));
                String meetings = "";
                foreach (Meeting meeting in Meetings.Values.Where(x => x.CanJoin(userId)).ToList())
                {
                    
                        meetings += meeting.ToString();
                }
                return meetings;
            }

            void IMSDADServer.CloseMeeting(String topic, String userId)
            {
                SafeSleep();

                bool found = Meetings.TryGetValue(topic, out Meeting meeting);
                
                if ((!found) || meeting.CurState != Meeting.State.Open) { 
                    throw new TopicDoesNotExistException("Topic " + topic + " cannot be closed\n");
                }
                
                lock (Location.Locations)
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
                        return (int) (y.GetNumUsers() - x.GetNumUsers());
                    });
                    
                    uint numUsers = slots[0].GetNumUsers();
                    DateTime date = slots[0].Date;

                    //Only those with maximum potential users
                    slots = slots.Where(x => x.GetNumUsers() == numUsers).ToList();

                    //Tightest room
                    slots.Sort((x, y) =>
                    {
                        return (int) (x.Location.GetBestFittingRoomForCapacity(date, numUsers).Capacity - y.Location.GetBestFittingRoomForCapacity(date, numUsers).Capacity);
                    });

                    meeting.Close(slots[0], numUsers);

                }
            }

            void IMSDADServerPuppet.AddRoom(String location, uint capacity, String roomName)
            {
                lock (this)
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
            void IMSDADServerPuppet.Crash() {
                System.Environment.Exit(1);
            }
            void IMSDADServerPuppet.Freeze() { }
            void IMSDADServerPuppet.Unfreeze() { }
            void IMSDADServerPuppet.Status() { }

            void IMSDADServer.NewClient(string url, string id)
            {
                
                registerNewClient(url, id);
                
                foreach(IMSDADServerToServer server in this.ServerURLs)
                {
                    server.registerNewClient(url, id);
                }
            }

            public HashSet<ServerClient> RegisterNewServer(string url)
            {
                lock (ClientURLs)
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
                }

                return ClientURLs;
               
            }

            public void registerNewClient(string url, string id)
            {
                lock (ClientURLs) {
                    this.ClientURLs.Add(new ServerClient(url, id));
                }
            }
        }
    }
}
