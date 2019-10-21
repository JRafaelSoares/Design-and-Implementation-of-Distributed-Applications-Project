using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSDAD
{
    namespace Server
    {
        class Meeting
        {
            private String CoordenatorID { get; }
            private String Topic { get; }
            private int MinParticipants { get; }
            private HashSet<Slot> Slots { get; }
            private HashSet<String> Invitees { get; } = new HashSet<String>();

            public Meeting(String coordenatorID, String topic, int minParticipants, HashSet<Slot> slots, HashSet<String> invitees = null)
            {
                this.CoordenatorID = coordenatorID;
                this.Topic = topic;
                this.MinParticipants = minParticipants;
                this.Slots = slots;

                if (invitees != null)
                {
                    this.Invitees = invitees;
                }
            }

            //Set Methods
            public override bool Equals(Object obj)
            {
                //Check for null and compare run-time types.
                if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                {
                    return false;
                }
                else
                {
                    Meeting r = (Meeting)obj;
                    return r.Topic == this.Topic;
                }
            }

            public override int GetHashCode()
            {
                return this.Topic.GetHashCode();
            }

        }

        class Slot
        {
            private Location Location { get; }
            private DateTime Date { get; }

            public Slot(Location location, DateTime date)
            {
                this.Location = location;
                this.Date = date;
            }

        }
    }
}
