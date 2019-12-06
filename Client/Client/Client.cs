using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;
using System.Timers;

namespace MSDAD
{
    namespace Client
    {
        public delegate String parseDelegate();

        class Client : MarshalByRefObject, IMSDADClientToClient, IMSDADClientPuppet
        {
            private static readonly int WAIT_TIME = 6000;
            
            private Dictionary<String, String> KnownServers { get; set; } = new Dictionary<string, String>();
            private String CurrentServerUrl;
            private String serverId;
            private IMSDADServer CurrentServer;
            private readonly String ClientId;
            private readonly String ClientURL;
            private int milliseconds;
            private Dictionary<String, Meeting> Meetings { get; set; } = new Dictionary<string, Meeting>();
            private static readonly Object CreateMeetingLock = new object();

            private string scriptName { get; set; }

            public Client(IMSDADServer server, String userId, String serverUrl, String ClientURL)
            {
                this.CurrentServer = server;
                this.serverId = server.GetServerID();
                this.ClientId = userId;
                this.ClientURL = ClientURL;
                this.milliseconds = 0;
                this.CurrentServerUrl = serverUrl;
            }

            private void ListMeetings()
            {
                SafeSleep();
                try
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    IDictionary<String, Meeting> receivedMeetings = CurrentServer.ListMeetings(this.Meetings.Where(x => x.Value.CanJoin(this.ClientId))
                        .ToDictionary(entry => entry.Key, entry => entry.Value));
                    foreach (Meeting meeting in receivedMeetings.Values)
                    {
                        this.Meetings[meeting.Topic] = meeting;

                    }

                    foreach (Meeting meeting in Meetings.Values)
                    {
                        Console.WriteLine(meeting.ToString());
                    }
                    stopWatch.Stop();

                    TimeSpan ts = stopWatch.Elapsed;
                    StreamWriter file = new StreamWriter("" + this.scriptName + this.ClientId + "results");
                    file.WriteLine("List time: " + ts);
                    file.Close();

                }
               catch (System.Net.Sockets.SocketException)
                {
                    this.ReconnectingClient();
                    this.ListMeetings();
                }

            }

            private void JoinMeeting(String topic, List<String> slots)
            {
                SafeSleep();
                try
                { 
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    //Join meeting 
                    CurrentServer.JoinMeeting(topic, slots, this.ClientId, DateTime.Now);
                    stopWatch.Stop();

                    TimeSpan ts = stopWatch.Elapsed;
                    StreamWriter file = new StreamWriter("" + this.scriptName + this.ClientId + "results");
                    file.WriteLine("Join time: " + ts);
                    file.Close();
                }
                catch (NoSuchMeetingException) {
                    // Server doesn't have the meeting yet, wait and try again later
                    Thread.Sleep(WAIT_TIME);
                    JoinMeeting(topic, slots);
                }
                catch (MSDAD.Shared.ServerException e)
                {
                    Console.WriteLine(e.GetErrorMessage());

                }
                catch (System.Net.Sockets.SocketException)
                {
                    this.ReconnectingClient();
                    this.JoinMeeting(topic, slots);
                }

            }

             private void CloseMeeting(String topic)
            {
                SafeSleep();
                try
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    CurrentServer.CloseMeeting(topic, this.ClientId);
                    stopWatch.Stop();

                    TimeSpan ts = stopWatch.Elapsed;
                    StreamWriter file = new StreamWriter("" + this.scriptName + this.ClientId + "results");
                    file.WriteLine("CloseMeeting time: " + ts);
                    file.Close();
                }
                catch (MSDAD.Shared.ServerException e)
                {
                    Console.Write(e.GetErrorMessage());
                }
                catch (System.Net.Sockets.SocketException)
                {
                    this.ReconnectingClient();
                    this.CloseMeeting(topic);

                }

            }

            private void CreateMeeting(String topic, uint min_atendees, List<String> slots, HashSet<String> invitees)
            {
                SafeSleep();
                try
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    Meeting meeting;

                    if (invitees == null)
                    {
                        meeting = new Meeting(this.ClientId, topic, min_atendees, slots);
                    }
                    else
                    {
                        meeting = new MeetingInvitees(this.ClientId, topic, min_atendees, slots, invitees);
                    }

                    CurrentServer.CreateMeeting(topic, meeting);
                    Console.WriteLine(String.Format("Meeting with topic {0} created at the server", topic));
                    Meetings.Add(topic, meeting);
                    Console.WriteLine(String.Format("Trying to gossip meeting with topic {0}", topic));
                    List<ServerClient> gossipClients = CurrentServer.GetGossipClients(ClientId);
                    Console.WriteLine(String.Format("got {0} clients to gossip meeting with topic {0}", gossipClients.Count, topic));
                    gossipMeeting(gossipClients, meeting, topic);
                    Console.WriteLine(String.Format("meeting with topic {0} gossiped", topic));

                    stopWatch.Stop();

                    TimeSpan ts = stopWatch.Elapsed;
                    StreamWriter file = new StreamWriter("" + this.scriptName + this.ClientId + "results");
                    file.WriteLine("CloseMeeting time: " + ts);
                    file.Close();
                }
                catch (System.Net.Sockets.SocketException)
                {
                    this.ReconnectingClient();
                    this.CreateMeeting(topic, min_atendees, slots, invitees);
                    
                } 

                catch (MSDAD.Shared.ServerException e)
                {
                    Console.WriteLine(e.GetErrorMessage());
                } 
            }

            private void Wait(int milliseconds)
            {
                SafeSleep();
                this.milliseconds = milliseconds;
            }

            private void SafeSleep()
            {
                if (this.milliseconds != 0)
                {
                    Thread.Sleep(this.milliseconds);
                    this.milliseconds = 0;
                }
            }

            public void ParseScript(parseDelegate reader, int state)
            {

                String line;

                while ((line = reader.Invoke()) != null)
                {
                    //To run each step, press Enter
                    if (state == 1 || Console.ReadKey().Key.Equals(ConsoleKey.Enter))
                    {
                        Console.WriteLine(line);
                        String[] items = line.Split(' ');
                        switch(items[0])
                        {
                            case "list":
                                this.ListMeetings();
                                break;

                            case "close":
                                this.CloseMeeting(items[1]);
                                break;

                            case "join":
                                List<String> slots = new List<string>();
                                uint slotCount = UInt32.Parse(items[2]);
                                for (uint i = 3; i < 3 + slotCount; ++i)
                                {
                                    slots.Add(items[i]);
                                }
                                this.JoinMeeting(items[1], slots);
                                break;

                            case "create":
                                int numSlots = Int32.Parse(items[3]);
                                int numInvitees = Int32.Parse(items[4]);

                                slots = new List<string>();
                                HashSet<String> invitees = numInvitees == 0 ? null : new HashSet<string>();
                                uint j;
                                for (j = 5; j < 5 + numSlots; ++j)
                                {
                                    slots.Add(items[j]);
                                }
                                for (; j < 5 + numSlots + numInvitees; ++j)
                                {
                                    invitees.Add(items[j]);
                                }
                                this.CreateMeeting(items[1], UInt32.Parse(items[2]), slots, invitees);
                                break;

                            case "wait":
                                this.Wait(Int32.Parse(items[1]));
                                break;

                            default:
                                Console.WriteLine("Invalid command: {0}", items[0]);
                                break;
                        }
                    }
                }
            }

            void IMSDADClientToClient.CreateMeeting(String topic, Meeting meeting)
            {
                lock (CreateMeetingLock)
                {
                    Meetings.Add(topic, meeting);
                }

            }

            Dictionary<String, Meeting> IMSDADClientToClient.SendMeetings()
            {
                return this.Meetings;
            }

            static void Main(string[] args)
            {
                if(args.Length != 6)
                {
                    System.Console.WriteLine("<usage> Client username client_port network_name server_url script_file client_ip");
                    Environment.Exit(1);
                }

                TcpChannel channel = new TcpChannel(Int32.Parse(args[1]));
                ChannelServices.RegisterChannel(channel, false);
                IMSDADServer server = (IMSDADServer)Activator.GetObject(typeof(IMSDADServer), args[3]);
                Console.WriteLine("This is the first server I'm connecting to!" + args[3]);

                if (server == null)
                {
                    System.Console.WriteLine("Server could not be contacted");
                    Environment.Exit(1);
                }
                else
                {
                    Console.WriteLine("CLIENT BEGINING");
                    String url = "tcp://" + args[5] + ":" +  args[1] + "/" + args[2];
                    Client client = new Client(server, args[0], args[3], url);
                    RemotingServices.Marshal(client, args[2], typeof(Client));

                    client.scriptName = args[4];
                    //Register Client with server
                    client.KnownServers = server.NewClient(url, args[0]);

                    string randomClientUrl = server.GetRandomClient(client.ClientId);
                    if(randomClientUrl != null)
                    {
                        Console.WriteLine("THE RANDOM CLIENT IS: " + randomClientUrl);
                        IMSDADClientToClient otherClient = (IMSDADClientToClient)Activator.GetObject(typeof(IMSDADClientToClient), randomClientUrl);
                        client.Meetings = otherClient.SendMeetings();
                    }
                    else
                    {
                        Console.WriteLine("NO OTHER CLIENT IN THE SYSTEM");
                    }

                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + args[4]))
                    {
                        StreamReader reader = File.OpenText(AppDomain.CurrentDomain.BaseDirectory + args[4]);
                        Console.WriteLine("Press R to run the entire script, or S to start run step by step. Enter Key to each step");
                        int state = 0;
                        //To run everything, press R
                        /*
                        if (Console.ReadLine().Equals("R"))
                        {
                            state = 1;
                        }
                        */
                        client.ParseScript(reader.ReadLine, 1);
                        reader.Close();
                    }
                    else
                    {
                        Console.WriteLine("Error: File provided does not exist");
                    }
                    client.ParseScript(Console.ReadLine, 1);
                }
            }

            public void ShutDown()
            {
                Environment.Exit(1);
            }

            public void ReconnectingClient()
            {
                Console.WriteLine("The server " + CurrentServerUrl + " crashed, reconnecting to a new server");

                //Choose random server to connect to
                Random r = new Random();
                String key = KnownServers.Keys.ToList()[r.Next(0, KnownServers.Count)];
                CurrentServerUrl = KnownServers[key];
                
                try
                {
                    Console.WriteLine("Trying to reconnect this server: " + CurrentServerUrl);
                    CurrentServer = (IMSDADServer)Activator.GetObject(typeof(IMSDADServer), CurrentServerUrl);
                    KnownServers = CurrentServer.NewClient(ClientURL, ClientId);
                    Console.WriteLine("The current server is now: " + CurrentServerUrl);

                } catch (System.Net.Sockets.SocketException) {
                    ReconnectingClient();
                } 
            }

            void gossipMeeting(List<ServerClient> clients, Meeting meeting, string topic)
            {
                if (clients == null)
                {
                    return;
                }

                foreach (ServerClient client in clients)
                {
                    Console.WriteLine(String.Format("Gossip metting with topic {0}to client with id{1}", topic, client.ClientId));
                    IMSDADClientToClient c = (IMSDADClientToClient)Activator.GetObject(typeof(IMSDADClientToClient), client.Url);
                    c.receiveGossipMeetings(meeting, topic);
                    Console.WriteLine(String.Format("Successfully gossiped metting with topic {0}to client with id{1}", topic, client.ClientId));
                }
            }

            void IMSDADClientToClient.receiveGossipMeetings(Meeting meeting, string topic)
            {
                Console.WriteLine(String.Format("Received meeting with topic {0} to gossip", topic));
                if (!Meetings.ContainsKey(topic))
                {
                    Console.WriteLine(String.Format("I didn't have meeting with topic {0} will also gossip", topic));
                    Meetings.Add(topic, meeting);
                    Console.WriteLine(String.Format("Get clients to gossip meeting with topic {0}", topic));
                    List<ServerClient> clients = CurrentServer.GetGossipClients(ClientId);
                    Console.WriteLine(String.Format("Gossiped meeting with topic {0}", topic));

                    gossipMeeting(clients, meeting, topic);
                }
                else
                {
                    Console.WriteLine(String.Format("Already seen meeting with topic {0} so no gossip from me", topic));
                }
            }
        }
    }
}
