using System;
using System.Collections.Generic;

namespace MSDAD
{
    namespace Server
    {
        class Slot
        {
            public Location Location { get; }
            public DateTime Date { get; }
            private List<String> UserIds;

            public Slot(Location location, DateTime date)
            {
                this.Location = location;
                this.Date = date;
            }

            public Slot(string slotString)
            {
                String[] items = slotString.Split(',');
                this.Location = Location.GetRoomFromName(items[0]);
                this.Date = DateTime.Parse(items[1]);
            }

            public void addUserId(String userId)
            {
                UserIds.Add(userId);
            }

            public bool Equals(Slot slot)
            {
                return this.Location == slot.Location && this.Date == slot.Date;
            }

            public static List<Slot> ParseSlots(List<String> slots)
            {
                List<Slot> hashSlot = new List<Slot>();

                foreach (string slot in slots)
                {
                    hashSlot.Add(new Slot(slot));
                }

                return hashSlot;
            }
        }

        class Meeting
        {
            public String CoordenatorID { get; }
            public String Topic { get; }
            public uint MinParticipants { get; }
            public List<Slot> Slots { get; }

            public Meeting(String coordenatorID, String topic, uint minParticipants, List<String> slots)
            {
                this.CoordenatorID = coordenatorID;
                this.Topic = topic;
                this.MinParticipants = minParticipants;
                this.Slots = Slot.ParseSlots(slots);
            }

            public virtual bool CanJoin(String userId)
            {
                return true;
            }

            public virtual String ToString(String userID)
            {
                return String.Format("{0}\n{1}\n{2}\n{3}\n\n", this.CoordenatorID, this.Topic, this.MinParticipants, this.Slots.ToString());
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

        class MeetingInvitees : Meeting
        {
            private HashSet<String> Invitees { get; } = new HashSet<String>();

            public MeetingInvitees(String coordenatorID, String topic, uint minParticipants, List<string> slots, HashSet<String> invitees) : base(coordenatorID, topic, minParticipants, slots)
            {
                this.Invitees = invitees;
            }

            public override bool CanJoin(string userId)
            {
                return Invitees.Contains(userId) || userId == this.CoordenatorID;
            }

            public override String ToString(String userID)
            {
                return base.ToString(userID) + Invitees.ToString();
            }

        }
    }
}
