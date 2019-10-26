using System;
using System.Collections.Generic;

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

            public Meeting(String coordenatorID, String topic, int minParticipants, HashSet<string> slots)
            {
                this.CoordenatorID = coordenatorID;
                this.Topic = topic;
                this.MinParticipants = minParticipants;
                this.Slots = ParseSlots(slots);
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

            public HashSet<Slot> ParseSlots(HashSet<string> slots)
            {
                HashSet<Slot> hashSlot = new HashSet<Slot>();

                foreach(string slot in slots)
                {
                    hashSlot.Add(new Slot(slot));
                }

                return hashSlot;
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

            public Slot(string slots)
            {

            }

        }
    }
}
