using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;
namespace MSDAD
{
    namespace Client
    {
        class Client
        {
            private readonly IMSDADServer Server;
            private readonly String UserId;
            public Client(IMSDADServer server, String userId)
            {
                this.Server = server;
                this.UserId = userId;
            }

            public void ListMeetings()
            {
                IList<String> meetings = Server.ListMeetings(this.UserId);
                
                foreach (String s in meetings)
                {
                    Console.WriteLine(s);   
                }
            }

            public void JoinMeeting(String topic, HashSet<String> slots)
            {
                Server.JoinMeeting(topic, slots, this.UserId);
            }

            public void CloseMeeting(String topic)
            {
                Server.CloseMeeting(topic, this.UserId);
            }

            public void CreateMeeting(String topic, uint min_atendees, HashSet<String> slots, HashSet<String> invitees)
            {
                Server.CreateMeeting(this.UserId, topic, min_atendees, slots, invitees);
            }

            public void Wait(int milliseconds)
            {
                Thread.Sleep(milliseconds);
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
                IMSDADServer server = (IMSDADServer)Activator.GetObject(typeof(IMSDADServer), args[3]);
                if (server == null)
                {
                    System.Console.WriteLine("Server could not be contacted");
                    Environment.Exit(1);
                }
                else
                {
                    Client client = new Client(server, args[2]);

                }


            }
        }
    }
}
