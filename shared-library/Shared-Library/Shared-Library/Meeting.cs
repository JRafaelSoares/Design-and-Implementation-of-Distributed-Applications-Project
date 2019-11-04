using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MSDAD
{
    namespace Shared
    {
        public class Slot
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
                if (this.Location == null)
                {
                    throw new LocationDoesNotExistException("Location given does not exist");
                }
                this.Date = DateTime.Parse(items[1]).Date;
            }

            public virtual new string ToString()
            {
                String s = String.Format("(Date:{0}, Location:{1})\nAtendees: ", Date.ToShortDateString(), Location.ToString());
                foreach (String u in UserIds)
                {
                    s += u + " ";
                }
                return s + "\n";
            }

            public void AddUserId(String userId)
            {
                UserIds.Add(userId);
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
                    return s.Date == this.Date && s.Location == this.Location;
                }
            }
        }

        public class ClosedSlot : Slot
        {

            //Room set when meeting is closed
            public Room Room { get; set; }

            public ClosedSlot(Slot slot, Room room) : base(slot.Location, slot.Date)
            {
                this.UserIds = slot.UserIds;
                this.Room = room;
            }

            public virtual new string ToString()
            {
                String s = String.Format("(Date:{0}, Location:{1}, Room: ({2}))\nAtendees: ", Date.ToShortDateString(), Location.ToString(), Room.ToString());
                foreach (String u in UserIds)
                {
                    s += u + " ";
                }
                return s + "\n";
            }

        }

        public class Meeting
        {
            public String CoordenatorID { get; }
            public String Topic { get; }
            public uint MinParticipants { get; }
            public List<Slot> Slots { get; set; }
            public List<String> Users;
            public enum State { Open, Closed, Canceled }
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

            public  virtual new String ToString()
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

            public void Close(Slot chosenSlot, uint numUsers)
            {
                this.CurState = State.Closed;

                Room bestRoom = chosenSlot.Location.GetBestFittingRoomForCapacity(chosenSlot.Date, numUsers);
                bestRoom.AddBooking(chosenSlot.Date);
                
                this.Slots = new List<Slot>{ new ClosedSlot(chosenSlot, bestRoom) };

                Users = Users.GetRange(0, (int)numUsers);
            }

        }

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
    
