using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MSDAD.Shared;

namespace MSDAD
{
    namespace Server
    {
        class Server : IMSDADServer
        {
            private HashSet<Meeting> Meetings;
            static void Main(string[] args)
            {

            }

            void IMSDADServer.CreateMeeting(string coordId, string topic, int minParticipants, ISet<string> slots, ISet<string> invitees)
            {

                throw new NotImplementedException();

                /*if (invitees == null)
                {
                    //meetings.Add(new Meeting(coordId, topic, minParticipants, slots));
                }
                else
                {
                    //meetings.Add(new Meeting(coordId, topic, minParticipants, slots, invitees));
                }*/
            }


            void IMSDADServer.JoinMeeting(String topic, ISet<string> slots, String userId)
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
