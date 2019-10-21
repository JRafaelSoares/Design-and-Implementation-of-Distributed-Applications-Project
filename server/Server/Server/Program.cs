using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared_Library;

namespace Server
{
    class Server : IMSDADServer
    {
        private HashSet<Meeting> meetings;
        static void Main(string[] args)
        {
        }

        public void createMeeting(String coordID, String topic, int minParticipants, HashSet<Slot> slots, HashSet<String> invitees = null)
        {
            if(invitees == null)
            {
                meetings.Add(new Meeting(coordID, topic, minParticipants, slots));
            }
            else
            {
                meetings.Add(new Meeting(coordID, topic, minParticipants, slots, invitees));
            }
        }
    }
}
