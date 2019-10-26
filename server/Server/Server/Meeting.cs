using System;
using System.Collections.Generic;

namespace MSDAD
{
    namespace Server
    {
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

            public virtual String ToString(String userID)
            {
                return String.Format("{0}\n{1}\n{2}\n{3}\n\n", this.CoordenatorID, this.Topic, this.MinParticipants, this.Slots.ToString());
            }

            public virtual bool CanJoin(String userId)
            {
                return true;
            }

            public List<Slot> GetSortedSlots()
            {
                Slots.Sort((x, y) => x.GetNumUsers().CompareTo(y.GetNumUsers()));

                return Slots;
            }
        }

        class Slot
        {
            public Location Location { get; }
            public DateTime Date { get; }

            public List<String> UserIds = new List<String>();

            public Slot(Location location, DateTime date)
            {
                this.Location = location;
                this.Date = date;
            }

            public Slot(string slots)
            {
                String[] items = slots.Split(',');
                this.Location = Location.GetRoomFromName(items[0]);
                this.Date = DateTime.Parse(items[1]);
            }

            public override string ToString()
            {
                string str = "(";
                str += Date.ToString() + ", " + Location.ToString() + ")";
                return str;
            }

            public void AddUserId(String userId)
            {
                UserIds.Add(userId);
            }

            public int GetNumUsers()
            {
                return UserIds.Count;
            }

            public Room GetAvailableRoom(uint minNumParticipants)
            {
                List<Room> rooms = Location.getOrderedRooms();

                foreach(Room room in rooms)
                {
                    if (room.Capacity < minNumParticipants)
                    {
                        return null;
                    }

                    if (!room.IsBooked(Date))
                    {
                        return room;
                    }
                }

                return null;
            }

            public void RemoveLastUsers(int usersToRemove)
            {
                UserIds.RemoveRange(UserIds.Count - usersToRemove, UserIds.Count);
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
                return (Invitees.Contains(userId) || userId == this.CoordenatorID);
            }
        }
    }
}
    
