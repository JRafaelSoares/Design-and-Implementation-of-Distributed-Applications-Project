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

            public MeetingInvitees(String coordenatorID, String topic, uint minParticipants, List<string> slots, HashSet<String> invitees) : base(coordenatorID, topic, minParticipants, slots)
            {
                this.Invitees = invitees;
            }

            public override String ToString(String userID)
            {
                return base.ToString(userID) + Invitees.ToString();
            }

            public override bool CanJoin(string userId)
            {
                return Invitees.Contains(userId) || userId == this.CoordenatorID;
            }
        }
    }
}

