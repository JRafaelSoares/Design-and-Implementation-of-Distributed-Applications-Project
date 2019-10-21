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
        private HashSet<Slot> slots { get; }
        private HashSet<String> invitees { get; } = new HashSet<String>();

        public Meeting(String coordenatorID, String topic, int minParticipants, HashSet<Slot> slots, HashSet<String> invitees = null)
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

    class Slot
    {
        private Location location { get; }
        private DateTime date { get; }

        public Slot(Location location, DateTime date)
        {
            this.location = location;
            this.date = date;
        }

    }
}
