using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MSDAD
{
    namespace Shared
    {
        [Serializable]
        public class Join : IComparable<Join>
        {
            public String UserId { get; }
            public DateTime Timestamp { get; }
            public ConcurrentDictionary<String, int> VectorClock { get; }

            public Join(String userId, DateTime timestamp)
            {
                this.UserId = userId;
                this.Timestamp = timestamp;
            }

            public Join(String userId, DateTime timestamp, ConcurrentDictionary<String, int> vectorClock) : this(userId, timestamp)
            {
                this.VectorClock = vectorClock;
            }

            public override string ToString()
            {
                return String.Format("({0},{1})", UserId.ToString(), Timestamp.ToString());
            }
            public override bool Equals(object obj)
            {
                if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                {
                    return false;
                }
                else
                {
                    Join j = (Join)obj;
                    return j.UserId == this.UserId;
                }
            }

            public int CompareTo(Join other)
            {
                int totalClockValue = 0;
                foreach(String key in this.VectorClock.Keys)
                {
                    totalClockValue += (int)(this.VectorClock[key] - other.VectorClock[key]);
                    //Subtracts one clock to the other;
                    //If only positives or 0, our join was after his, return 1;
                    //If only negatives or 0, our join was before his, return -1;
                    //Else concurrent
                }
                return totalClockValue; 
                
            }
        }

        [Serializable]
        public class Slot
        {
            public String LocationString { get; set; }
            public Location Location { get { return Location.Locations[this.LocationString]; } }
            public DateTime Date { get; }

            public List<Join> UserIds = new List<Join>();
            
            public Slot(String location, DateTime date)
            {
                this.LocationString = location;
                this.Date = date.Date;
            }

            public Slot(string slots)
            {
                String[] items = slots.Split(',');
                this.LocationString = items[0];
                this.Date = DateTime.Parse(items[1]).Date;
            }

            public virtual new string ToString()
            {
                String s = String.Format("(Date:{0}, Location:{1})\nAtendees: ", Date.ToShortDateString(), this.LocationString);
                foreach (Join u in UserIds)
                {
                    s += u.ToString() + " ";
                }
                return s + "\n";
            }

            public void AddUserId(String userId, DateTime timestamp, ConcurrentDictionary<String, int> vectorClock)
            {
                UserIds.Add(new Join(userId, timestamp, vectorClock));
            }

            public uint GetNumUsers()
            {
                Room avaliable = GetBestAvaliableRoom();
                uint roomCapacity = avaliable == null ? 0 : avaliable.Capacity;
                return Math.Min((uint)UserIds.Count, roomCapacity);
            }

            public Room GetBestAvaliableRoom()
            {
                return Location.GetBestRoomForDate(this.Date.Date);
            }


            public Room GetAvailableRoom(uint minNumParticipants)
            {
                return Location.Rooms.Where(x => x.Capacity >= minNumParticipants).First(x => !x.IsBooked(Date));

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

            public override bool Equals(Object obj)
            {
                //Check for null and compare run-time types.
                if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                {
                    return false;
                }
                else
                {
                    Slot s = (Slot)obj;
                    return s.Date == this.Date && s.LocationString == this.LocationString;
                }
            }
        }

        [Serializable]
        public class ClosedSlot : Slot
        {

            //Room set when meeting is closed
            public Room Room { get; set; }

            public ClosedSlot(Slot slot, Room room, List<Join> joins) : base(slot.LocationString, slot.Date)
            {
                this.UserIds = slot.UserIds;
                this.Room = room;
                this.UserIds = joins;
            }

            public virtual new string ToString()
            {
                String s = String.Format("(Date:{0}, Location:{1}, Room: ({2}))\nAtendees: ", Date.ToShortDateString(), LocationString, Room.ToString());
                foreach (Join u in UserIds)
                {
                    s += u.ToString() + " ";
                }
                return s + "\n";
            }

        }

        [Serializable]
        public class Meeting
        {
            public String CoordenatorID { get; }
            public String Topic { get; }
            public uint MinParticipants { get; }
            public List<Slot> Slots { get; set; }
            public List<Join> Users;
            public List<Join> UsersNotJoined { get; set; } = new List<Join>();
            public enum State { Open = 1, Pending = 2, Closed = 3, Canceled = 4 }
            public State CurState { get; set; }

            public Meeting(String coordenatorID, String topic, uint minParticipants, List<String> slots)
            {
                this.CoordenatorID = coordenatorID;
                this.Topic = topic;
                this.MinParticipants = minParticipants;
                this.Slots = Slot.ParseSlots(slots);
                this.Users = new List<Join>();
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

            public virtual new String ToString()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(String.Format("Coordenator: {0}\n", this.CoordenatorID));
                builder.Append(String.Format("Topic: {0}\n", this.Topic));
                builder.Append(String.Format("MinParticipants: {0}\n", this.MinParticipants));
                builder.Append("Slots:\n");
                foreach (Slot s in this.Slots)
                {
                    if (CurState == State.Closed)
                    {

                        builder.Append(((ClosedSlot)s).ToString() + "\n");
                    }
                    else
                    {

                        builder.Append(s.ToString() + "\n");
                    }
                }
                builder.Append("Users:\n");
                foreach (Join u in this.Users)
                {
                    builder.Append(u.ToString() + "\n");
                }
                builder.Append(String.Format("State: {0}\n", this.CurState.ToString()));
                
                builder.Append("Users that did not fit:\n");
                foreach (Join u in this.UsersNotJoined)
                {
                        builder.Append(u.ToString() + "\n");
                }

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

            public void AddUser(String UserId, DateTime timestamp, ConcurrentDictionary<String, int> vectorClock)
            {
                Users.Add(new Join(UserId, timestamp, vectorClock));
            }

            public void BookClosedMeeting()
            {
                if (this.CurState == State.Closed)
                {
                    Room bestRoom = this.Slots[0].Location.GetBestFittingRoomForCapacity(this.Slots[0].Date, (uint)this.Users.Count);
                    bestRoom.AddBooking(this.Slots[0].Date);
                }
            }

            public void Close(Slot chosenSlot, uint numUsers)
            {
                this.CurState = State.Closed;

                Room bestRoom = chosenSlot.Location.GetBestFittingRoomForCapacity(chosenSlot.Date, numUsers);
                bestRoom.AddBooking(chosenSlot.Date);

                //FIXME Use this when we are sending the Vector clocks!!!
                //Users.Sort();

                Users.Sort((x, y) =>
                {
                    return x.Timestamp <= y.Timestamp ? -1 : 1;
                });
                this.UsersNotJoined = Users.GetRange((int)numUsers, Users.Count - (int)numUsers);
                Users = Users.GetRange(0, (int)numUsers);

                this.Slots = new List<Slot> { new ClosedSlot(chosenSlot, bestRoom, Users) };
            }

            

            public Meeting MergeMeeting(Meeting other)
            {
                if (this.CurState >= Meeting.State.Closed)
                {
                    return this;
                }
                else if (other.CurState >= Meeting.State.Closed)
                {
                    return other;
                }
                else
                {
                    if (this.CurState > other.CurState)
                    {
                        return this;
                    }
                    else if (this.CurState < other.CurState)
                    {
                        return other;
                    }
                    else
                    {
                        foreach (Join user in this.Users)
                        {
                            if (!other.Users.Contains(user))
                            {
                                other.Users.Add(user);
                            }
                        }
                        foreach (Slot slot in this.Slots)
                        {
                            Slot mySlot = other.Slots.First(s => s.Equals(slot));

                            foreach (Join user in slot.UserIds)
                            {
                                if (!mySlot.UserIds.Contains(user))
                                {
                                    mySlot.UserIds.Add(user);
                                }
                            }
                        }
                        return other;
                    }
                }
            }
        }

        [Serializable]
        public class MeetingInvitees : Meeting
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
                foreach (String invitee in Invitees)
                {
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



