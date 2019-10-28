using System;
using System.Collections.Generic;
using System.Linq;

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
                this.Date = date;
            }

            public Slot(string slots)
            {
                String[] items = slots.Split(',');
                this.Location = Location.FromName(items[0]);
                this.Date = DateTime.Parse(items[1]);
            }

            public override string ToString()
            {
                return String.Format("({0},{1})", Date.ToString(), Location.ToString());
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
        }

        class Meeting
        {
            public String CoordenatorID { get; }
            public String Topic { get; }
            public uint MinParticipants { get; }
            public List<Slot> Slots { get; }
            public List<String> Users = new List<String>();
            public enum State { Open, Closed }
            private State curState = State.Open;

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

            //Falta testar os ToStrings das listas e se nao funcionar acrescenta-se metodo

            public virtual String ToString(String userID)
            {
                return String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n\n", this.CoordenatorID, this.Topic, this.MinParticipants, this.Slots.ToString(), this.Users.ToString(), this.curState.ToString("g"));
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
                this.curState = State.Closed;
            }

            public State getState()
            {
                return this.curState;
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
    
