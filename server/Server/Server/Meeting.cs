using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Meeting
    {
        private String coordenatorID { get; }
        private String topic { get; }
        private int minParticipants { get; }
        private HashSet<Slots> slots { get; }
        private HashSet<String> invitees { get; } = new HashSet<String>();

        public Meeting(String coordenatorID, String topic, int minParticipants, HashSet<Slots> slots, HashSet<String> invitees = null)
        {
            this.coordenatorID = coordenatorID;
            this.topic = topic;
            this.minParticipants = minParticipants;
            this.slots = slots;
            if (invitees != null)
            {
                this.invitees = invitees;
            }
        }

    }
}
