using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.Remoting.Messaging;

namespace MSDAD
{
    namespace Server
    {
        class Server : MarshalByRefObject, IMSDADServer, IMSDADServerPuppet, IMSDADServerToServer
        {
            /*************************************************************************************************************/
            /*****************************************************Properties*********************************************/
            /***********************************************************************************************************/
            /*System members*/
            private ConcurrentDictionary<String, IMSDADServerToServer> ServerView = new ConcurrentDictionary<String, IMSDADServerToServer>();
            private readonly ConcurrentDictionary<String, String> ServerNames = new ConcurrentDictionary<String, String>();
            private readonly ConcurrentDictionary<ServerClient, byte> ClientURLs = new ConcurrentDictionary<ServerClient, byte>();
            /*****************************/

            /*Server properties*/
            private readonly ConcurrentDictionary<String, Meeting> Meetings = new ConcurrentDictionary<string, Meeting>();
            private readonly String ServerId;
            private readonly String ServerUrl;
            private readonly uint MaxFaults;
            private readonly int MinDelay;
            private readonly int MaxDelay;
            /****************************/

            private bool LeaderToken { get; set; }
            private static readonly Object CloseMeetingLock = new object();
            private static readonly Random random = new Random();

            /*Delegates for Async calls*/
            public delegate ConcurrentDictionary<String, Meeting> ListAsyncDelegate();
            public delegate Meeting RemoteAsyncDelegate(String topic);
            public delegate void JoinAsyncDelegate(String topic, List<string> slots, String userId, DateTime timestamp);
            public delegate void MergeMeetingDelegate(String topic, Meeting meeting);
            public delegate void PingDelegate();
            /************************************/

            /*Properties for Reliable Broadcast*/
            public delegate void RBSendDelegate(String messageId, String operation, object[] args);
            public ConcurrentDictionary<String, CountdownEvent> RBMessages = new ConcurrentDictionary<string, CountdownEvent>();
            public int RBMessageCounter = 0;
            /***********************************/

            /*Properties for Fault Detection*/
            private readonly int maxFailures = 3;
            private TimeSpan timeout
            {
               get
                {
                    return TimeSpan.FromMilliseconds((MaxDelay * 2) + 1000);
                }
            }
            private ConcurrentDictionary<String, int> CountFails = new ConcurrentDictionary<String, int>();
            private ConcurrentDictionary<String, TimeSpan> Timeouts = new ConcurrentDictionary<String, TimeSpan>();
            /**********************************/

            /*********Propertiesfor Causal Order****************/
            private ConcurrentDictionary<String, int> VectorClock = new ConcurrentDictionary<string, int>();
            /***************************************************/



            /**********************************************************************************************************************/
            /****************************************************Functions*******************************************************/
            /********************************************************************************************************************/

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

                Console.WriteLine(String.Format("[SETUP] Server with id {0} Initializing  with url {1}", args[1], ServerUrl));

                //Initialize Server
                TcpChannel channel = new TcpChannel(Int32.Parse(args[3]));
                ChannelServices.RegisterChannel(channel, false);
                Server server = new Server(args[1], UInt32.Parse(args[4]), Int32.Parse(args[5]), Int32.Parse(args[6]), ServerUrl);
                RemotingServices.Marshal(server, args[2], typeof(Server));

                //Get Server URLS and connect to them
                int i;
                for (i = 8; i < 8 + Int32.Parse(args[7]); ++i)
                {
                    Console.WriteLine("[SETUP] Connecting to server with url {0}", args[i]);

                    IMSDADServerToServer otherServer = (IMSDADServerToServer)Activator.GetObject(typeof(IMSDADServer), args[i]);
                    if (otherServer != null)
                    {
                        //FIXME Review: we don't need Server State since all the servers are created on the beginning
                        // of the system and none crashes while the system is setup
                        Console.WriteLine(String.Format("[SETUP] Successfully connected to server with url {0} successfully", args[i]));
                        String id = otherServer.NewServer(server.ServerId, server.ServerUrl);
                        server.CountFails.TryAdd(id, 0);
                        server.Timeouts.TryAdd(id, server.timeout);
                        server.ServerView[id] = otherServer;
                        server.ServerNames.TryAdd(id, args[i]);
                        server.VectorClock.TryAdd(id, 0);
                        Console.WriteLine(String.Format("[SETUP] server with url {0} has id {1} and has been added to my view", args[i], id));

                    }
                    else
                    {
                        //Should never happen
                        Console.WriteLine(String.Format("[ERROR] Could not connect to server at address {0}", args[i]));
                    }

                }

                //Means that it is the first server to be created
                if (server.ServerView.Count == 0)
                {
                    server.LeaderToken = true;
                }

                //Create Locations
                int j = i + 1;

                for (i = j; i < j + 3 * Int32.Parse(args[j - 1]); i += 3)
                {
                    Console.WriteLine(String.Format("[SETUP] Adding room: {0} {1} {2}", args[i], args[i + 1], args[i + 2]));
                    ((IMSDADServerPuppet)server).AddRoom(args[i], UInt32.Parse(args[i + 1]), args[i + 2]);
                }

                Console.WriteLine(String.Format("[SETUP] Setup successfull!\n[SETUP] [FINISH] ip: {0} ServerId: {1} network_name: {2} port: {3} max faults: {4} min delay: {5} max delay: {6}", args[0], args[1], args[2], args[3], args[4], args[5], args[6]));
                Console.WriteLine("[SHUTDOWN] Press < enter > to shutdown server...");


                //Start Failure Detector
                foreach (String otherServer in server.ServerView.Keys)
                {
                    server.CountFails.TryAdd(otherServer, 0);
                }

                /*Thread t = new Thread(new ThreadStart(server.FailureDetector));
                t.IsBackground = true;
                t.Start();*/

                Console.ReadLine();
            }

            public Server(String ServerId, uint MaxFaults, int MinDelay, int MaxDelay, String ServerUrl)
            {
                this.ServerId = ServerId;
                this.MaxFaults = MaxFaults;
                this.MinDelay = MinDelay;
                this.MaxDelay = MaxDelay;
                this.ServerUrl = ServerUrl;
                this.VectorClock.TryAdd(this.ServerId, 0);
            }


            /***********************************************************************************************************************/
            /*********************************************************Aux***********************************************************/
            /***********************************************************************************************************************/

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
            //Aux function to merge lists of meetings
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
            void PropagateClosedMeeting(String topic, Meeting meeting)
            {
                SafeSleep();
                Object objLock = new Object();
                CountdownEvent latch = new CountdownEvent(this.ServerView.Count);

                //Propagate the closed meeting to all the servers and await for all responses
                foreach (IMSDADServerToServer otherServer in this.ServerView.Values)
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

            /***********************************************************************************************************************/
            /*************************************************Client Server*********************************************************/
            /***********************************************************************************************************************/

            HashSet<ServerClient> IMSDADServer.CreateMeeting(string topic, Meeting meeting)
            {
                Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][NEW-MEETING][CAUSAL-SEND] Broadcast meeting with topic {0} to other servers", topic));
                //FIXME We should make it causally ordered
                Send_CausalOrder("CreateMeeting", new object[] { topic, meeting });
                //this.RB_Broadcast(RBNextMessageId(), "CreateMeeting", new object[] { topic, meeting });
                Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][NEW-MEETING][FINISH] Meeting with topic {0} broadcasted successfully", topic));
                return this.GetMeetingInvitees(this.Meetings[topic]);
            }
            
            void IMSDADServer.JoinMeeting(string topic, List<string> slots, string userId, DateTime timestamp)
            {
                Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][JOIN-MEETING] User {0} wants to join meeting with topic {1}", userId, topic));
                bool found = Meetings.TryGetValue(topic, out Meeting meeting);
                //Meeting hasn't reached the server yet
                if (!found)
                {
                    Console.WriteLine(String.Format("[ERROR][CLIENT-TO-SERVER][JOIN-MEETING] Meeting with topic {0} hasn't reached this server yet, so user {1} must retry later", topic, userId));
                    throw new NoSuchMeetingException("Meeting specified does not exist on this server");
                }
                //FIXME SEE IF RB_SEND CALLS ONE SELF

                Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][JOIN-MEETING][CAUSAL-SEND] Propagate join of user {0} to meeting with topic {1} to {2} servers", userId, topic, this.MaxFaults));

                Send_CausalOrder("JoinMeeting", new object[] { topic, slots, userId, timestamp });

                Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][JOIN-MEETING][FINISH] User {0} joined meeting with topic {1} finished", userId, topic));
                return;
            }
            
            IDictionary<String, Meeting> IMSDADServer.ListMeetings(Dictionary<String, Meeting> meetings)
            {
                //FIXME JUST MERGE AND RETURN
                SafeSleep();
                Console.WriteLine("[INFO][CLIENT-TO-SERVER][LIST-MEETINGS] Received List Meetings request");
                ListMeetingsMerge(meetings);

                CountdownEvent latch = new CountdownEvent((int)this.MaxFaults);
                Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][LIST-MEETINGS][QUERY] Will query {0} servers before return", this.MaxFaults));

                foreach (IMSDADServerToServer otherServer in this.ServerView.Values)
                {
                    ListAsyncDelegate RemoteDel = new ListAsyncDelegate(otherServer.GetMeetings);
                    AsyncCallback RemoteCallback = new AsyncCallback(ar =>
                    {
                        ListAsyncDelegate del = (ListAsyncDelegate)((AsyncResult)ar).AsyncDelegate;
                        ConcurrentDictionary<String, Meeting> serverMeeting = del.EndInvoke(ar);
                        lock (latch)
                        {
                            //Only merge meetings for f servers, then return
                            if (!latch.IsSet)
                            {

                                Console.WriteLine(String.Format("[ACK][CLIENT-TO-SERVER][LIST-MEETINGS] Ack of Get Meetings only {0} to go", latch.CurrentCount - 1));
                                ListMeetingsMerge(serverMeeting);
                                latch.Signal();
                            }
                        }
                    }
                    );
                    IAsyncResult RemAr = RemoteDel.BeginInvoke(RemoteCallback, null);
                }
                latch.Wait();
                //Cannot dispose latch because callback uses it

                Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][LIST-MEETINGS][FINISH] List meetings query finished", this.MaxFaults));
                return this.Meetings;
            }
            
            Dictionary<String, String> IMSDADServer.NewClient(string url, string id)
            {
                Console.WriteLine("[INFO][CLIENT-TO-SERVER][NEW-CLIENT] Received New Client connection request: client: <id:{0} ; url:{1}>", id, url);

                ServerClient client = new ServerClient(url, id);

                if (!ClientURLs.ContainsKey(client))
                {
                    Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][NEW-CLIENT] First time seeing client <id:{0} ; url:{1}>, will Broadcast", id, url));

                    this.RB_Broadcast(RBNextMessageId(), "NewClient", new object[] { client });
                }
                else
                {
                    //Only need to sleep if I am not broadcasting
                    SafeSleep();
                    Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][NEW-CLIENT] Already know about client <id:{0} ; url:{1}>, do not need to broadcast", id, url));
                }

                Console.WriteLine(String.Format("[INFO][CLIENT-TO-SERVER][NEW-CLIENT][FINISH] Client <id:{0} ; url:{1}> connected successfully, will give known servers urls", id, url));
                //Give Known Servers to Client
                Dictionary<string, string> tempServerNames = ServerNames.ToDictionary(entry => entry.Key, entry => entry.Value);
                tempServerNames.Remove(ServerId);
                return tempServerNames;
            }
            
            String IMSDADServer.getRandomClient(String clientId)
            {
                SafeSleep();
                KeyValuePair<ServerClient, byte> t = this.ClientURLs.FirstOrDefault(x => x.Key.ClientId != clientId);
                return t.Equals(null) ? t.Key.Url : null;
            }
            
            void IMSDADServer.CloseMeeting(string topic, string userId)
            {
                SafeSleep();
                Console.WriteLine(String.Format("Client with id {0} wants to close meeting with topic {1}", userId, topic));
                lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                {
                    Object objLock = new Object();
                    CountdownEvent latch = new CountdownEvent(this.ServerView.Count);
                    //Lock users from joining local meeting
                    this.Meetings[topic].CurState = Meeting.State.Pending;

                    //Lock users from joining a meeting on the other servers
                    foreach (IMSDADServerToServer otherServer in this.ServerView.Values)
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
                        IMSDADServerToServer leader = this.ServerView[this.ServerView.Keys.Min()];
                        MergeMeetingDelegate del = new MergeMeetingDelegate(leader.CloseMeeting);
                        del.BeginInvoke(topic, this.Meetings[topic], null, null);
                    }
                }
            }

            /***********************************************************************************************************************/
            /*************************************************Server Puppet*********************************************************/
            /***********************************************************************************************************************/
            void IMSDADServerPuppet.AddRoom(String location, uint capacity, String roomName)
            {
                Console.WriteLine(String.Format("[INFO][SERVER-PUPPET][ADD-ROOM] Received new room: location {0}, room {1} capacity {2}", location, roomName, capacity));
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
                Console.WriteLine(String.Format("[INFO][SERVER-PUPPET][ADD-ROOM][FINISH] Room location {0}, room {1} capacity {2} finished", location, roomName, capacity));

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
                foreach (KeyValuePair<String, String> server in this.ServerNames)
                {
                    Console.WriteLine(String.Format("[STATUS] Server with id {0} is in the current view at url {1}", server.Key, server.Value));
                }
            }
            void IMSDADServerPuppet.ShutDown()
            {
                Environment.Exit(1);
            }
            /***********************************************************************************************************************/
            /*************************************************Server to Server******************************************************/
            /***********************************************************************************************************************/

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

            void IMSDADServerToServer.Ping()
            {
                SafeSleep();
                return;
            }

            void IMSDADServerToServer.JoinMeeting(String topic, List<String> slots, String userId, DateTime timestamp)
            {
                Console.WriteLine(String.Format("[INFO][SERVER-TO-SERVER][JOIN-MEETING] Join of user {0} to meeting with topic {1} reached this server", userId, topic));
                bool found = Meetings.TryGetValue(topic, out Meeting meeting);

                //FIXME Join will be causal with create and as such this will never happen
                if (!found)
                {
                    Console.WriteLine(String.Format("[ERROR][SERVER-TO-SERVER][JOIN-MEETING] Join of user {0} to meeting with topic {1} " +
                        "cannot be processed as meeting as not reached the server", userId, topic));
                    return;
                    //throw new NoSuchMeetingException("Meeting specified does not exist on this server");
                }

                //See if Meeting as reached the server yet
                if (meeting.CurState != Meeting.State.Open)
                {
                    return;
                    //FIXME Maybe just return since this is called by another server?
                    //throw new CannotJoinMeetingException("Meeting is no longer open");
                }

                //Join Client to Meeting
                lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                {
                    if (!meeting.CanJoin(userId))
                    {
                        //FIXME Just print error info since it should not happen
                        Console.WriteLine(String.Format("[ERROR][SERVER-TO-SERVER][JOIN-MEETING] Client {0} will join meeting with topic {1} without an invite", userId, topic));
                        //throw new CannotJoinMeetingException("User " + userId + " cannot join this meeting.\n");
                    }

                    List<Slot> givenSlots = Slot.ParseSlots(slots);
                    foreach (Slot slot in meeting.Slots.Where(x => givenSlots.Contains(x)))
                    {

                        slot.AddUserId(userId, timestamp, this.VectorClock);

                    }
                    meeting.AddUser(userId, timestamp, this.VectorClock);
                }
                Console.WriteLine(String.Format("[INFO][SERVER-TO-SERVER][JOIN-MEETING][FINISH] user {0} joined meeting with topic {1}", userId, topic));
                return;
            }

            void IMSDADServerToServer.MergeClosedMeeting(string topic, Meeting meeting)
            {
                SafeSleep();
                lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                {
                    //Update Meeting and book room if Meeting was closed
                    Meetings[topic] = meeting;
                    meeting.BookClosedMeeting();
                }
            }

            //Aux functions for list meetings
            ConcurrentDictionary<String, Meeting> IMSDADServerToServer.GetMeetings()
            {
                SafeSleep();
                Console.WriteLine("[INFO][SERVER-TO-SERVER][LIST-MEETINGS][FINISH] Got request to send my meetings");
                return Meetings;
            }

            void IMSDADServerToServer.CreateMeeting(String topic, Meeting meeting)
            {
                Console.WriteLine(String.Format("[INFO][SERVER-TO-SERVER][NEW-MEETING] Meeting with topic {0} reached server with id {1}", topic, this.ServerId));
                Meetings.TryAdd(topic, meeting);
                Console.WriteLine(String.Format("[INFO][SERVER-TO-SERVER][NEW-MEETING][FINISH] Meeting with topic {0} added to server with id {1}", topic, this.ServerId));

            }

            Meeting IMSDADServerToServer.LockMeeting(string topic)
            {
                SafeSleep();
                //Lock meeting as fase one of closing a meeting
                lock (Meetings.Keys.FirstOrDefault(k => k.Equals(topic)))
                {
                    this.Meetings[topic].CurState = Meeting.State.Pending;
                    return Meetings[topic];
                }
            }

            void IMSDADServerToServer.NewClient(ServerClient client)
            {
                Console.WriteLine(String.Format("[INFO][SERVER-TO-SERVER][NEW-CLIENT][RELIABLE-BROADCAST] Broadcast of client <id:{0} ; url:{1}> reached server {2}", client.ClientId, client.Url, this.ServerId));
                this.ClientURLs.TryAdd(client, new byte());
                Console.WriteLine(String.Format("[INFO][SERVER-TO-SERVER][NEW-CLIENT][FINISH] Client <id:{0} ; url:{1}> added to server {2}", client.ClientId, client.Url, this.ServerId));
            }

            String IMSDADServerToServer.NewServer(String id, string url)
            {
                Console.WriteLine("[INFO][SERVER-TO-SERVER][NEW-SERVER] Trying to Connect to server at address {0}", url);
                IMSDADServerToServer otherServer = (IMSDADServerToServer)Activator.GetObject(typeof(IMSDADServer), url);
                if (otherServer != null)
                {
                    CountFails.TryAdd(id, 0);
                    Timeouts.TryAdd(id, this.timeout);
                    ServerView.TryAdd(id, otherServer);
                    ServerNames.TryAdd(id, url);
                    VectorClock.TryAdd(id, 0);
                    Console.WriteLine("[INFO][SERVER-TO-SERVER][NEW-SERVER][FINISH] Successfully connected to server at address {0}", url);
                }
                else
                {
                    Console.WriteLine("[ERROR][SERVER-TO-SERVER][NEW-SERVER] Cannot connect to server at address {0}", url);
                }
                return this.ServerId;
            }

            /***********************************************************************************************************************/
            /*************************************************Failure Detector******************************************************/
            /***********************************************************************************************************************/

            public async void FailureDetector()
            {
                while (true)
                {
                    foreach (KeyValuePair<String, IMSDADServerToServer> pair in ServerView)
                    {
                        Action<object> action = (object obj) => { pair.Value.Ping(); };
                        Task task = new Task(action, null);
                        task.Start();

                        if (await Task.WhenAny(task, Task.Delay(Timeouts[pair.Key])) != task)
                        {
                            // timeout logic
                            CountFails[pair.Key] += 1;
                            Timeouts[pair.Key] = TimeSpan.FromMilliseconds(Timeouts[pair.Key].TotalMilliseconds + 500);
                            Console.WriteLine("[FAILURE-DETECTOR] Server " + pair.Key + " did not ping back, happened {0} times, next timeout: {1} miliseconds",
                                                                                                CountFails[pair.Key], Timeouts[pair.Key].TotalMilliseconds);

                        }
                        else
                        {
                            // task completed within timeout
                            CountFails[pair.Key] = 0;
                            Timeouts[pair.Key] = timeout;
                        }

                        if (CountFails[pair.Key] == maxFailures)
                        {
                            Console.WriteLine("[FAILURE-DETECTOR] Server " + pair.Key + " failed");
                            ServerView.TryRemove(pair.Key, out _);
                        }

                    }
                    Thread.Sleep(1000 * 10);
                }
            }


            /***********************************************************************************************************************/
            /*************************************************Causal Order Broadcast************************************************/
            /***********************************************************************************************************************/

            void Send_CausalOrder(string operation, object[] args)
            {
                ConcurrentDictionary<String, int> vec;
                lock (this.VectorClock)
                {
                    this.VectorClock[this.ServerId]++;
                    vec = new ConcurrentDictionary<String, int>(this.VectorClock);
                }
                Console.WriteLine(String.Format("[CAUSAL-ORDER] Send message for operation {0} with clock {1}", operation, vec));
                RB_Broadcast(RBNextMessageId(), "Deliver_CausalOrder", new object[] { vec, operation, args });

            }

            void IMSDADServerToServer.Deliver_CausalOrder(ConcurrentDictionary<String, int> clock, string operation, object[] args)
            {
                //Do not need to sleep as this is delivered from RB
                int rand_id = random.Next();
                Console.WriteLine(String.Format("[CAUSAL-ORDER] Received message for operation {0} with id {1}", operation, rand_id));
                while (true)
                {
                    lock (VectorClock)
                    {

                        int clockDiference = 0;
                        string clockId = "";
                        foreach (String id in VectorClock.Keys)
                        {
                            if (VectorClock[id] < clock[id])
                            {
                                clockDiference += VectorClock[id] - clock[id];
                                clockId = id;
                            }
                        }

                        Console.WriteLine(String.Format("[CAUSAL-ORDER] Clock diference for operation {0} with id {2} is {1}", operation, clockDiference, rand_id));

                        if (clockDiference < -1)
                        {
                            Console.WriteLine(String.Format("[CAUSAL-ORDER] Still need to wait for messages as clocks do not match for operation {0} with id {1} (is {2} and must be -1)", operation, rand_id, clockDiference));
                            Monitor.Wait(VectorClock);
                            Console.WriteLine(String.Format("[CAUSAL-ORDER] operation {0} with id {1} has been notified of changing of clocks, will recalculate diference", operation, rand_id));

                        }
                        else
                        {
                            Console.WriteLine(String.Format("[CAUSAL-ORDER] operation {0} with id {1} can now be executed", operation, rand_id));
                            //For the case he sends to itself don't need to increment
                            if (clockDiference != 0)
                            {
                                VectorClock[clockId]++;
                                Console.WriteLine(String.Format("[CAUSAL-ORDER] Message with id {0} has updated the clock, will notify all pending messages", operation, rand_id));
                                Monitor.PulseAll(VectorClock);
                            }
                            break;
                        }
                    }
                }
                Console.WriteLine(String.Format("[CAUSAL-ORDER] Can now deliver message for operation {0} with id {1}", operation, rand_id));
                GetType().GetInterface("IMSDADServerToServer").GetMethod(operation).Invoke(this, args);
            }

            /***********************************************************************************************************************/
            /*************************************************Reliable Broadcast****************************************************/
            /***********************************************************************************************************************/

            private String RBNextMessageId()
            {
                return String.Format("{0}-{1}", this.ServerId, Interlocked.Increment(ref this.RBMessageCounter));
            }

            //This method is called by someone who wants to broadcast (IE Doesn't need to sleep and 
            //knows that the message is new as it is the one he is sending
            void RB_Broadcast(string messageId, string operation, object[] args)
            {
                Console.WriteLine(String.Format("[RELIABLE-BROADCAST] Send message with id {0} for operation {1}, wait for {2} acks", messageId, operation, MaxFaults));
                RBMessages.TryAdd(messageId, new CountdownEvent((int)MaxFaults));
                foreach (IMSDADServerToServer server in this.ServerView.Values)
                {
                    RBSendDelegate remoteDel = new RBSendDelegate(server.RB_Send);
                    IAsyncResult RemAr = remoteDel.BeginInvoke(messageId, operation, args, null, null);
                }
                RBMessages[messageId].Wait();
                GetType().GetInterface("IMSDADServerToServer").GetMethod(operation).Invoke(this, args);
            }

            //This method is called by Reliable Broadcast when a message is received from another server and then broadcast back (IE it needs to sleep)
            void IMSDADServerToServer.RB_Send(string messageId, string operation, object[] args)
            {
                SafeSleep();
                if (RBMessages.TryAdd(messageId, new CountdownEvent((int)MaxFaults)))
                {
                    Console.WriteLine(String.Format("[RELIABLE-BROADCAST-SEND] Received message with id {0} for operation {1} for the first time, wait for {2} acks", messageId, operation, MaxFaults));

                    //First time seeing this message, rebroadcast and wait for F acks
                    foreach (IMSDADServerToServer server in this.ServerView.Values)
                    {
                        RBSendDelegate remoteDel = new RBSendDelegate(server.RB_Send);
                        IAsyncResult RemAr = remoteDel.BeginInvoke(messageId, operation, args, null, null);
                    }
                    //Already received first ack
                    RBMessages[messageId].Signal();
                    Console.WriteLine(String.Format("[RELIABLE-BROADCAST-SEND] ACK for message with id {0} for operation {1}. Need to wait for {2} more acks", messageId, operation, RBMessages[messageId].CurrentCount));
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
                            Console.WriteLine(String.Format("[RELIABLE-BROADCAST-SEND] ACK for message with id {0} for operation {1}. Need to wait for {2} more acks", messageId, operation, RBMessages[messageId].CurrentCount));

                        }
                    }
                }
            }

        }
    }
}