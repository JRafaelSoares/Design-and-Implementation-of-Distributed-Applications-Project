using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace MSDAD
{
    namespace Server
    {
        class Server : MarshalByRefObject, IMSDADServer, IMSDADServerPuppet
        {
            private Dictionary<String, Meeting> Meetings = new Dictionary<string, Meeting>();
            static void Main(string[] args)
            {
                TcpChannel channel = new TcpChannel(8086);
                ChannelServices.RegisterChannel(channel, false);
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(Server), "MSDADServer", WellKnownObjectMode.Singleton);
                System.Console.WriteLine(" Press < enter > to shutdown server...");
                System.Console.ReadLine();
            }

            void IMSDADServer.CreateMeeting(string coordId, string topic, uint minParticipants, List<string> slots, HashSet<string> invitees)
            {

                if (invitees == null)
                {
                    Meetings.Add(topic, new Meeting(coordId, topic, minParticipants, slots));
                }
                else
                {
                    Meetings.Add(topic, new MeetingInvitees(coordId, topic, minParticipants, slots, invitees));
                }
            }


            void IMSDADServer.JoinMeeting(String topic, List<String> slots, String userId)
            {

                Meeting m = Meetings[topic];
                if (!m.CanJoin(userId))
                {
                    throw new CannotJoinMeetingException("User " + userId + " cannot join this meeting.");
                }
                List<Slot> MeetingSlots = m.Slots;
                List<Slot> ClientSlots = Slot.ParseSlots(slots);

                foreach (Slot cslot in ClientSlots)
                {
                    foreach (Slot mslot in MeetingSlots)
                    {
                        if (cslot.Equals(mslot))
                        {
                            mslot.AddUserId(userId);
                        }
                    }
                }

            }

            String IMSDADServer.ListMeetings(String userId)
            {
                String meetings = "";
                foreach (Meeting meeting in Meetings.Values)
                {
                    if (meeting.CanJoin(userId))
                    {
                        meetings += meeting.ToString();
                    }
                }

                return meetings;
            }

            String IMSDADServer.CloseMeeting(String topic, String userId)
            {
                Meeting meeting;
                try
                {
                    meeting = Meetings[topic];
                }
                catch (KeyNotFoundException)
                {
                    throw new TopicDoesNotExistException("Topic " + topic + " does not exist");
                }

                if (meeting.CoordenatorID != userId)
                {
                    throw new ClientNotCoordenatorException("Client " + userId + " is not this topic Coordenator.");
                }

                List<Slot> slots = meeting.GetSortedSlots();

                foreach (Slot slot in slots)
                {
                    if (slot.GetNumUsers() < meeting.MinParticipants)
                    {
                        Meetings.Remove(topic);
                        throw new NoMeetingAvailableException("No meeting meets the requirements. Meeting Canceled");
                    }

                    Room room = slot.GetAvailableRoom(meeting.MinParticipants);

                    if (room == null)
                    {
                        continue;
                    }

                    //removes the last users to join (can be problematic in distributed/ order lists?)
                    if (room.Capacity < slot.GetNumUsers())
                    {
                        slot.RemoveLastUsers(slot.GetNumUsers() - (int)room.Capacity);
                    }

                    room.AddBooking(slot.Date);
                    Meetings.Remove(topic);
                    return String.Format("Meeting booked for date: {0}, location: {1} and room: {2}.", slot.Date, slot.Location.Name, room.Name);
                }

                Meetings.Remove(topic);
                throw new NoMeetingAvailableException("No meeting meets the requirements. Meeting Canceled");

            }

            void IMSDADServerPuppet.addRoom(String location, uint capacity, String roomName)
            {
                lock (this)
                {
                    Location local = Location.GetRoomFromName(location);
                    if (local == null)
                    {
                        local = new Location(location);
                        Location.addLocation(local);
                    }
                    local.addRoom(new Room(roomName, capacity));
                }

            }
        }
    }
}
