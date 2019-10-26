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
                if (slots != null)
                {
                    Meeting m = Meetings[topic];
                    List<Slot> ServerSlots = m.ParseSlots(slots);

                    foreach (Slot s in ServerSlots)
                    {
                        s.addUserId(userId);
                    }
                }

            }

            String IMSDADServer.ListMeetings(String userId)
            {
                String meetings = "";
                foreach(Meeting meeting in Meetings.Values)
                {
                    meetings += meeting.ToString();
                }

                return meetings;
            }

            String IMSDADServer.CloseMeeting(String topic, String userId)
            {
                Meeting meeting = Meetings[topic];

                if(meeting.CoordenatorID != userId)
                {
                    return "You do not have permission to close this meeting!";
                }

                List<Slot> slots = meeting.getSortedSlots();

                foreach(Slot slot in slots)
                {
                    if (slot.getNumUsers() < meeting.MinParticipants)
                    {
                        Meetings.Remove(topic);
                        return "No meeting could be booked. Meeting Canceled";
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
                return "No meeting could be booked. Meeting Canceled";

            }

        }
    }
}
