using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSDAD
{

    namespace Server
    {
        class MeetingInvitees : Meeting
        {
            private HashSet<String> Invitees { get; } = new HashSet<String>();

            public MeetingInvitees(String coordenatorID, String topic, int minParticipants, List<String> slots, HashSet<String> invitees) : base(coordenatorID, topic, minParticipants, slots)
            {
                this.Invitees = invitees;
            }
        }
    }
}

