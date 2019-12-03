﻿using MSDAD.Shared;
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
            private IMSDADServer CurrentServer;
            private readonly String ClientId;
            private readonly String ClientURL;
            private int milliseconds;
            private readonly Dictionary<String, Meeting> Meetings = new Dictionary<string, Meeting>();
            private static readonly Object CreateMeetingLock = new object();


            public Client(IMSDADServer server, String userId, String serverUrl, String ClientURL)
            {
                this.CurrentServer = server;
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

                } catch (RemotingTimeoutException) {
                    ReconnectingClient();
                }

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
                catch (RemotingTimeoutException)
                {
                    ReconnectingClient();
                }
            }

            private void CloseMeeting(String topic)
            {
                SafeSleep();
                try
                {
                    CurrentServer.CloseMeeting(topic, this.ClientId);
                } catch(MSDAD.Shared.ServerException e)
                {
                    Console.Write(e.GetErrorMessage());
                } catch (RemotingTimeoutException) {
                    ReconnectingClient();
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

                    //Propagate Meeting
                    HashSet<ServerClient> clients = CurrentServer.CreateMeeting(topic, meeting);
                    
                    foreach(ServerClient client in clients)
                    {
                        if (client.ClientId != ClientId)
                        {
                            IMSDADClientToClient otherClient = (IMSDADClientToClient)Activator.GetObject(typeof(IMSDADClientToClient), client.Url);
                            otherClient.CreateMeeting(topic, meeting);
                        }
                    }
                    Meetings.Add(topic, meeting);
                
                } catch (System.Net.Sockets.SocketException e)
                {
                    ReconnectingClient();
                }
                catch (RemotingTimeoutException) {
                    ReconnectingClient();
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
                    String url = "tcp://" + args[5] + ":" +  args[1] + "/" + args[2];
                    Client client = new Client(server, args[0], args[3], url);
                    RemotingServices.Marshal(client, args[2], typeof(Client));

                    //Register Client with server
                    client.KnownServers = server.NewClient(url, args[0]);

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
                Console.WriteLine("The server crashed, reconnecting to a new server");

                Random r = new Random();

                int i = r.Next(0, KnownServers.Count);

                //Choose random server to connect to

                Dictionary<string, string>.KeyCollection keyColl = KnownServers.Keys;
                foreach (string s in keyColl)
                {
                    if (i != 0)
                    {
                        i--;
                        continue;
                    }
                    CurrentServerUrl = KnownServers[s];
                }

                CurrentServer = (IMSDADServer)Activator.GetObject(typeof(IMSDADServer), CurrentServerUrl);
                KnownServers = CurrentServer.NewClient(ClientURL, ClientId);

                Console.WriteLine("The current server is now: " + CurrentServerUrl);

               /* //Testing only
                foreach (String s in KnownServers.Keys)
                {
                    Console.WriteLine("This is the second server i'm connecting to: " + s);
                }*/

            }
        }
    }
}
