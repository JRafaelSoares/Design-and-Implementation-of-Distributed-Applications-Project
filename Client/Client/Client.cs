using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;

namespace MSDAD
{
    namespace Client
    {
        public delegate String parseDelegate();

        class Client : MarshalByRefObject, IMSDADClientToClient, IMSDADClientPuppet
        {

            private static readonly int WAIT_TIME = 3000;
            
            private Dictionary<String, String> KnownServers { get; set; } = new Dictionary<string, String>();
            private String CurrentServerUrl;
            private String serverId;
            private IMSDADServer CurrentServer;
            private readonly String ClientId;
            private readonly String ClientURL;
            private int milliseconds;
            private Dictionary<String, Meeting> Meetings { get; set; } = new Dictionary<string, Meeting>();
            private static readonly Object CreateMeetingLock = new object();

            public Client(IMSDADServer server, String userId, String serverUrl, String ClientURL)
            {
                this.CurrentServer = server;
                this.serverId = server.getServerID();
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
                    IDictionary<String, Meeting> receivedMeetings = CurrentServer.ListMeetings(this.Meetings);
                    foreach (Meeting meeting in receivedMeetings.Values)
                    {
                        this.Meetings[meeting.Topic] = meeting;

                    }

                    foreach (Meeting meeting in Meetings.Values)
                    {
                        Console.WriteLine(meeting.ToString());
                    }

                }
               catch (System.Net.Sockets.SocketException)
                {
                    this.ReconnectingClient();
                    this.ListMeetings();
                }
                /*catch (RemotingTimeoutException) {
                    this.ReconnectingClient();
                    this.ListMeetings();
                } */

            }

            private void JoinMeeting(String topic, List<String> slots)
            {
                SafeSleep();
                try
                {   
                    //Join meeting 
                    CurrentServer.JoinMeeting(topic, slots, this.ClientId, DateTime.Now);
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
                catch (System.Net.Sockets.SocketException e)
                {
                    this.ReconnectingClient();
                    this.JoinMeeting(topic, slots);
                }
                catch (RemotingTimeoutException)
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
                    CurrentServer.CloseMeeting(topic, this.ClientId);
                }
                catch (MSDAD.Shared.ServerException e)
                {
                    Console.Write(e.GetErrorMessage());
                }
                catch (System.Net.Sockets.SocketException e)
                {
                    this.ReconnectingClient();
                    this.CloseMeeting(topic);

                }
                catch (RemotingTimeoutException)
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
                    Meeting meeting;

                    if (invitees == null)
                    {
                        meeting = new Meeting(this.ClientId, topic, min_atendees, slots);
                        }
                    else
                    {
                        meeting = new MeetingInvitees(this.ClientId, topic, min_atendees, slots, invitees);
                    }

                    Meetings.Add(topic, meeting);

                    gossipMeeting(CurrentServer.getGossipClients(ClientId), meeting, topic);
                
                } catch (System.Net.Sockets.SocketException e)
                {
                    this.ReconnectingClient();
                    this.CreateMeeting(topic, min_atendees, slots, invitees);
                    
                } 
                catch (RemotingTimeoutException) {
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

                    //Register Client with server
                    client.KnownServers = server.NewClient(url, args[0]);

                    string randomClientUrl = server.getRandomClient(client.ClientId);
                    if(randomClientUrl != null)
                    {
                        Console.WriteLine("THE RANDOM CLIENT IS: " + randomClientUrl);
                        IMSDADClientToClient otherClient = (IMSDADClientToClient)Activator.GetObject(typeof(IMSDADClientToClient), randomClientUrl);
                        client.Meetings = otherClient.SendMeetings();
                    }

                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + args[4]))
                    {
                        StreamReader reader = File.OpenText(AppDomain.CurrentDomain.BaseDirectory + args[4]);
                        Console.WriteLine("Press R to run the entire script, or S to start run step by step. Enter Key to each step");
                        int state = 0;
                        //To run everything, press R
                        if (Console.ReadLine().Equals("R"))
                        {
                            state = 1;
                        }

                        client.ParseScript(reader.ReadLine, state);
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
                Console.WriteLine("Known servers size before removing: " + KnownServers.Count);
                foreach (String serverURl in KnownServers.Values)
                {
                    Console.WriteLine("I know this server: " + serverURl);
                }

                Console.WriteLine("Previous server id: " + serverId);

                Random r = new Random();
                
                //Choose random server to connect to
                String key = KnownServers.Keys.ToList()[r.Next(0, KnownServers.Count)];
                CurrentServerUrl = KnownServers[key];
                
                try
                {
                    Console.WriteLine("Trying to reconnect to: " + CurrentServerUrl);

                    CurrentServer = (IMSDADServer)Activator.GetObject(typeof(IMSDADServer), CurrentServerUrl);
                    KnownServers = CurrentServer.NewClient(ClientURL, ClientId);

                   
                    Console.WriteLine("The current server is now: " + CurrentServerUrl);

                } catch (System.Net.Sockets.SocketException e)
                {
                    ReconnectingClient();

                } /*catch (RemotingTimeoutException e)
                {
                    ReconnectingClient();
                }*/
            }

            void gossipMeeting(List<ServerClient> clients, Meeting meeting, string topic)
            {
                if (clients == null)
                {
                    return;
                }

                foreach (ServerClient client in clients)
                {
                    IMSDADClientToClient c = (IMSDADClientToClient)Activator.GetObject(typeof(IMSDADClientToClient), client.Url);
                    c.receiveGossipMeetings(meeting, topic);
                }
            }

            void IMSDADClientToClient.receiveGossipMeetings(Meeting meeting, string topic)
            {
                if (!Meetings.ContainsKey(topic))
                {
                    Meetings.Add(topic, meeting);
                    List<ServerClient> clients = CurrentServer.getGossipClients(ClientId);

                    gossipMeeting(clients, meeting, topic);
                }
            }
        }
    }
}
