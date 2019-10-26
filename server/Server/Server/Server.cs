using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace MSDAD
{
    namespace Server
    {
        class Server : MarshalByRefObject, IMSDADServer
        {
            private Dictionary<String, Meeting> Meetings;
            static void Main(string[] args)
            {
                TcpChannel channel = new TcpChannel(8086);
                ChannelServices.RegisterChannel(channel, false); 
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(Server), "MSDADServer", WellKnownObjectMode.Singleton);
                System.Console.WriteLine(" Press < enter > to shutdown server...");
                System.Console.ReadLine();

            }

            void IMSDADServer.CreateMeeting(string coordId, string topic, int minParticipants, HashSet<string> slots, HashSet<string> invitees)
            {

                if (invitees == null)
                {
                    Meetings.Add(topic, new Meeting(coordId, topic, minParticipants, slots));
                }
                else
                {
                    Meetings.Add(topic, new MeetingInvitees(coordId, topic, minParticipants, slots, invitees));
                }
            }


            void IMSDADServer.JoinMeeting(String topic, HashSet<string> slots, String userId)
            {

                throw new NotImplementedException();
            }

            IList<String> IMSDADServer.ListMeetings(String userId)
            {
                throw new NotImplementedException();
            }

            void IMSDADServer.CloseMeeting(String topic, String userId)
            {

                throw new NotImplementedException();
            }

        }
    }
}
