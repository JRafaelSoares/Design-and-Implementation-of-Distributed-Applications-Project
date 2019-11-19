using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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
            private readonly IMSDADServer Server;
            private readonly String UserId;
            private int milliseconds;
            private readonly Dictionary<String, Meeting> Meetings = new Dictionary<string, Meeting>();
            private static readonly Object CreateMeetingLock = new object();


            public Client(IMSDADServer server, String userId)
            {
                this.Server = server;
                this.UserId = userId;
                this.milliseconds = 0;
            }

            private void ListMeetings()
            {
                SafeSleep();

                Dictionary<String, Meeting> received = Server.ListMeetings(this.Meetings);
                foreach(Meeting recv in received.Values)
                {
                    this.Meetings[recv.Topic] = recv;

                }

                foreach(Meeting recv in Meetings.Values)
                {
                    Console.WriteLine(recv.ToString());
                }
            }

            private void JoinMeeting(String topic, List<String> slots)
            {
                SafeSleep();
                try
                {   
                    //Join meeting and update local meeting
                    Meeting meeting =  Server.JoinMeeting(topic, slots, this.UserId, DateTime.Now);
                    this.Meetings[meeting.Topic] = meeting;
                }
                catch (NoSuchMeetingException) {
                    // Server doesn't have the meeting yet, wait and try again later
                    Thread.Sleep(500);
                    JoinMeeting(topic, slots);
                }
                catch (MSDAD.Shared.ServerException e)
                {
                    Console.WriteLine(e.GetErrorMessage());

                }
            }

            private void CloseMeeting(String topic)
            {
                SafeSleep();
                try
                {
                    Server.ClientCloseMeeting(topic, this.UserId);
                } catch(MSDAD.Shared.ServerException e)
                {
                    Console.Write(e.GetErrorMessage());
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
                        meeting = new Meeting(this.UserId, topic, min_atendees, slots);
                        Meetings.Add(topic, meeting);
                    }
                    else
                    {
                        meeting = new MeetingInvitees(this.UserId, topic, min_atendees, slots, invitees);
                        Meetings.Add(topic, meeting);
                    }

                    HashSet<ServerClient> clients = Server.CreateMeeting(topic, meeting);

                    foreach(ServerClient client in clients)
                    {
                        if (client.ClientId != UserId)
                        {
                            IMSDADClientToClient otherClient = (IMSDADClientToClient)Activator.GetObject(typeof(IMSDADClientToClient), client.Url);
                            otherClient.CreateMeeting(topic, meeting);
                        }
                    }

                } catch(CannotCreateMeetingException e)
                {
                    Console.WriteLine(e.GetErrorMessage());
                } catch (LocationDoesNotExistException e)
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

            public void ParseScript(parseDelegate reader)
            {
                String line;
                while(( line = reader.Invoke() ) != null)
                {
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
                            HashSet<String> invitees =  numInvitees == 0 ?  null : new HashSet<string>();
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


                if (server == null)
                {
                    System.Console.WriteLine("Server could not be contacted");
                    Environment.Exit(1);
                }
                else
                {


                    Client client = new Client(server, args[0]);
                    RemotingServices.Marshal(client, args[2], typeof(Client));

                    //Register Client with server
                    server.NewClient("tcp://" + args[5] + ":" +  args[1] + "/" + args[2], args[0]);

                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + args[4]))
                    {
                        StreamReader reader = File.OpenText(AppDomain.CurrentDomain.BaseDirectory +  args[4]);
                        client.ParseScript(reader.ReadLine);
                        reader.Close();
                       
                    }
                    else
                    {
                        Console.WriteLine("Error: File provided does not exist");
                    }
                    client.ParseScript(Console.ReadLine);
                }
            }

            public void ShutDown()
            {
                Environment.Exit(1);
            }
        }
    }
}
