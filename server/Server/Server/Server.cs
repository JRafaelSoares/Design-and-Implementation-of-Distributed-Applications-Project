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
        class Server : MarshalByRefObject, IMSDADServer
        {
            private Dictionary<String, Meeting> Meetings;
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
                List<Slot> ClientSlots = m.ParseSlots(slots);

                foreach (Slot cslot in ClientSlots)
                {
                    foreach (Slot mslot in MeetingSlots)
                    {
                        if (cslot.Equals(mslot))
                        {
                            mslot.addUserId(userId);
                        }
                    }
                }    

            }

            String IMSDADServer.ListMeetings(String userId)
            {
                String meetings = "";
                foreach(Meeting meeting in Meetings.Values)
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
                } catch (KeyNotFoundException)
                {
                    throw new TopicDoesNotExistException("Topic " + topic + " does not exist");
                }

                if(meeting.CoordenatorID != userId)
                {
                    throw new ClientNotCoordenatorException("Client " + userId + " is not this topic Coordenator.");
                }

                List<Slot> slots = meeting.getSortedSlots();

                foreach(Slot slot in slots)
                {
                    if (slot.getNumUsers() < meeting.MinParticipants)
                    {
                        Meetings.Remove(topic);
                        throw new NoMeetingAvailableException("No meeting meets the requirements. Meeting Canceled");
                    }

                    Room room = slot.getAvailableRoom(meeting.MinParticipants);

                    if (room == null)
                    {
                        continue;
                    }

                    //removes the last users to join (can be problematic in distributed/ order lists?)
                    if (room.Capacity < slot.getNumUsers())
                    {
                        slot.removeLastUsers(slot.getNumUsers() - (int)room.Capacity);
                    }

                    room.addBooking(slot.Date);
                    Meetings.Remove(topic);
                    return String.Format("Meeting booked for date: {0}, location: {1} and room: {2}.", slot.Date, slot.Location.Name, room.Name);
                }

                Meetings.Remove(topic);
                throw new NoMeetingAvailableException("No meeting meets the requirements. Meeting Canceled");

            }

        }
    }
}
