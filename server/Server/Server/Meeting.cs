using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MSDAD
{
    namespace Server
    {
        class Slot
        {
            public Location Location { get; }
            public DateTime Date { get; }

            public List<String> UserIds = new List<String>();

            public Slot(Location location, DateTime date)
            {
                this.Location = location;
                this.Date = date.Date;
            }

            public Slot(string slots)
            {
                String[] items = slots.Split(',');
                this.Location = Location.FromName(items[0]);
                this.Date = DateTime.Parse(items[1]).Date;
            }

            public override string ToString()
            {
                return String.Format("({0},{1})", Date.ToShortDateString(), Location.ToString());
            }

            public void AddUserId(String userId)
            {
                UserIds.Add(userId);
            }

            public uint GetNumUsers()
            {
                return (uint)UserIds.Count;
            }

            public Room GetAvailableRoom(uint minNumParticipants)
            {
                return Location.Rooms.Where(x => x.Capacity >= minNumParticipants).First(x => !x.IsBooked(Date));

            }

            public Room GetRoomClosestNumParticipants(uint numParticipants)
            {
                List<Room> rooms = Location.Rooms.Where(x => !x.IsBooked(Date)).ToList();
                rooms.Sort((x, y) => x.Capacity.CompareTo(y.Capacity));

                Room closestRoom = null;
                foreach(Room room in Location.Rooms)
                {
                    if (room.Capacity == numParticipants)
                    {
                        return room;
                    }

                    if (room.Capacity < numParticipants)
                    {
                        if(closestRoom == null)
                        {
                            return room;
                        }

                        else
                        {
                            if (Math.Abs(numParticipants-room.Capacity) > Math.Abs(closestRoom.Capacity-numParticipants))
                            {
                                return closestRoom;
                            }
                            else
                            {
                                return room;
                            }
                        }
                    }

                    closestRoom = room;
                }

                return closestRoom;
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

        class Meeting
        {
            public String CoordenatorID { get; }
            public String Topic { get; }
            public uint MinParticipants { get; }
            public List<Slot> Slots { get; }
            public List<String> Users;
            public enum State { Open, Closed }
            public State CurState { get; set;}

            public Meeting(String coordenatorID, String topic, uint minParticipants, List<String> slots)
            {
                this.CoordenatorID = coordenatorID;
                this.Topic = topic;
                this.MinParticipants = minParticipants;
                this.Slots = Slot.ParseSlots(slots);
                this.Users = new List<String>();
                this.CurState = State.Open;
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

            public  virtual String ToString()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(String.Format("Coordenator: {0}\n", this.CoordenatorID));
                builder.Append(String.Format("Topic: {0}\n", this.Topic));
                builder.Append(String.Format("MinParticipants: {0}\n", this.MinParticipants));
                builder.Append("Slots:\n");
                foreach (Slot s in this.Slots)
                {
                    builder.Append(s.ToString() + "\n");
                }
                builder.Append("Users:\n");
                foreach (String u in this.Users)
                {
                    builder.Append(u + "\n");
                }
                builder.Append(String.Format("State: {0}\n", this.CurState.ToString()));
                return builder.ToString();
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

            public void AddUser(String UserId)
            {
                Users.Add(UserId);
            }

            public void Close()
            {
                this.CurState = State.Closed;
            }
        }

        class MeetingInvitees : Meeting
        {
            public HashSet<String> Invitees { get; }

            public MeetingInvitees(String coordenatorID, String topic, uint minParticipants, List<string> slots, HashSet<String> invitees) : base(coordenatorID, topic, minParticipants, slots)
            {
                this.Invitees = invitees;
            }

            public override String ToString()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("Invitees:\n");
                foreach (String invitee in Invitees) {
                    builder.Append(invitee + "\n");
                }
                return base.ToString() + builder.ToString(); ;
            }

            public override bool CanJoin(string userId)
            {
                return (Invitees.Contains(userId) || userId == this.CoordenatorID);
            }

        }
    }
}
    
