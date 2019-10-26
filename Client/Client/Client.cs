using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;
namespace MSDAD
{
    namespace Client
    {
        public delegate String parseDelegate();

        class Client
        {
            private readonly IMSDADServer Server;
            private readonly String UserId;

            public Client(IMSDADServer server, String userId)
            {
                this.Server = server;
                this.UserId = userId;
            }

            private void ListMeetings()
            {
                String meetings = Server.ListMeetings(this.UserId);
                
                Console.WriteLine(meetings);   
                
            }

            private void JoinMeeting(String topic, List<String> slots)
            {
                try
                {
                    Server.JoinMeeting(topic, slots, this.UserId);
                } catch (ServerException e)
                {
                    Console.WriteLine(e.getErrorMessage());
                }
            }

            private void CloseMeeting(String topic)
            {
                try
                {
                    Server.CloseMeeting(topic, this.UserId);
                } catch(ServerException e)
                {
                    Console.Write(e.getErrorMessage());
                } 
            }

            private void CreateMeeting(String topic, uint min_atendees, List<String> slots, HashSet<String> invitees)
            {
                Server.CreateMeeting(this.UserId, topic, min_atendees, slots, invitees);
            }

            private void Wait(int milliseconds)
            {
                Thread.Sleep(milliseconds);
            }

            public void ParseScript(parseDelegate reader)
            {
                String line = null;
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
                            for (uint i = 2; i < items.Length; ++i)
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

            static void Main(string[] args)
            {
                if(args.Length != 5)
                {
                    System.Console.WriteLine("<usage> Client username client_url server_url script_file");
                    Environment.Exit(1);
                }

                TcpChannel channel = new TcpChannel();
                ChannelServices.RegisterChannel(channel, false);
                IMSDADServer server = (IMSDADServer)Activator.GetObject(typeof(IMSDADServer), args[2]);
                if (server == null)
                {
                    System.Console.WriteLine("Server could not be contacted");
                    Environment.Exit(1);
                }
                else
                {
                    Client client = new Client(server, args[2]);
                  
                    if (File.Exists(args[3]))
                    {
                        client.ParseScript(File.OpenText(args[3]).ReadLine);
                       
                    }
                    else
                    {
                        Console.WriteLine("Error: File provided does not exist");
                    }
                    client.ParseScript(Console.ReadLine);
                    
                    

                }


            }
        }
    }
}
