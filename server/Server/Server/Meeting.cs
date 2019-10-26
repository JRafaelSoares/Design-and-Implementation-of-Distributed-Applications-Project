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
            private uint MinParticipants { get; }
            private List<Slot> Slots { get; }

            public Meeting(String coordenatorID, String topic, uint minParticipants, List<String> slots)
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
                List<Slot> hashSlot = new List<Slot>();

                foreach(string slot in slots)
                {
                    hashSlot.Add(new Slot(slot));
                }

                return hashSlot;
            }

            public virtual String ToString(String userID)
            {
                return String.Format("{0}\n{1}\n{2}\n{3}\n\n", this.CoordenatorID, this.Topic, this.MinParticipants, this.Slots.ToString());
            }

            public virtual bool CanJoin(String userId)
            {
                return true;
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

            public Slot(string slots)
            {

            }

            public void addUserId(String userId)
            {
                UserIds.Add(userId);
            }


        }
    }
}
