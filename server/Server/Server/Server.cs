using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Linq;
using System.Threading;

namespace MSDAD
{
    namespace Server
    {
        class Server : MarshalByRefObject, IMSDADServer, IMSDADServerPuppet
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

            public Server(String ServerId, uint MaxFaults, int MinDelay, int MaxDelay)
            {
                this.SeverId = ServerId;
                this.MaxFaults = MaxFaults;
                this.MinDelay = MinDelay;
                this.MaxDelay = MaxDelay;
                
            }

            static void Main(string[] args)
            {
                if(args.Length != 5)
                {
                    Console.WriteLine("<Usage> Server server_id port max_faults min_delay max_delay");
                    return;
                }

                //Initialize Server
                TcpChannel channel = new TcpChannel(Int32.Parse(args[1]));
                ChannelServices.RegisterChannel(channel, false);
                Server server = new Server(args[0], UInt32.Parse(args[2]), Int32.Parse(args[3]), Int32.Parse(args[4]));
                RemotingServices.Marshal(server, "MSDADServer", typeof(IMSDADServer));
                //Testing purposes only
                IMSDADServerPuppet puppet = (IMSDADServerPuppet) server;
                puppet.AddRoom("Lisbon", 5, "1");
                puppet.AddRoom("Lisbon", 1, "3");
                puppet.AddRoom("Lisbon", 3, "4");
                puppet.AddRoom("Porto", 2, "1");
                puppet.AddRoom("Lisbon", 2, "2");
                System.Console.WriteLine(String.Format("ServerId: {0} port: {1} max faults: {2} min delay: {3} max delay: {4}", args[0], args[1], args[2], args[3], args[4]));
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

            void IMSDADServer.CreateMeeting(string coordId, string topic, uint minParticipants, List<string> slots, HashSet<string> invitees)
            {
                Thread.Sleep(random.Next(MinDelay, MaxDelay));
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


            void IMSDADServer.JoinMeeting(String topic, List<String> slots, String userId)
            {
                Thread.Sleep(random.Next(MinDelay, MaxDelay));
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
                       
                        slot.AddUserId(userId);
                        
                    }
                    meeting.AddUser(userId);
                }
            }

            String IMSDADServer.ListMeetings(String userId)
            {
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
                Thread.Sleep(random.Next(MinDelay, MaxDelay));

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
        }
    }
}
