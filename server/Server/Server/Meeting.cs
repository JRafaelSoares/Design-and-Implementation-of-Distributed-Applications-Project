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
            private List<Slot> Slots { get; }

            public Meeting(String coordenatorID, String topic, int minParticipants, List<String> slots)
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

            public List<Slot> ParseSlots(List<String> slots)
            {
                List<Slot> listSlot = new List<Slot>();

                foreach(string slot in slots)
                {
                    listSlot.Add(new Slot(slot));
                }

                return listSlot;
            }

        }

        class Slot
        {
            private Location Location { get; }
            private DateTime Date { get; }

            private List<String> UserIds;

            public Slot(Location location, DateTime date)
            {
                this.Location = location;
                this.Date = date;
            }

            public Slot(String slot)
            {

            }

            public void addUserId(String userId)
            {
                UserIds.Add(userId);
            }

        }
    }
}
