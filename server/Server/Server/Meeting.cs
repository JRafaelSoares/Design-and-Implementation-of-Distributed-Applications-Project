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

            public List<Slot> getSortedSlots()
            {
                Slots.Sort((x, y) => x.getNumUsers().CompareTo(y.getNumUsers()));

                return Slots;
            }
        }

        class Slot
        {
            public Location Location { get; }
            public DateTime Date { get; }

            public List<String> UserIds;

            public Slot(Location location, DateTime date)
            {
                this.Location = location;
                this.Date = date;
            }

            public Slot(string slots)
            {
                //TODO
            }

            public override string ToString()
            {
                string str = "(";
                str += Date.ToString() + ", " + Location.ToString() + ")";
                return str;
            }

            public void addUserId(String userId)
            {
                UserIds.Add(userId);
            }

            public int getNumUsers()
            {
                return UserIds.Count;
            }

            public Room getAvailableRoom(uint minNumParticipants)
            {
                List<Room> rooms = Location.getOrderedRooms();

                foreach(Room room in rooms)
                {
                    if (room.Capacity < minNumParticipants)
                    {
                        return null;
                    }

                    if (!room.isBooked(Date))
                    {
                        return room;
                    }
                }

                return null;
            }

            public void removeLastUsers(int usersToRemove)
            {
                UserIds.RemoveRange(UserIds.Count - usersToRemove, UserIds.Count);
            }
        }
    }
}
