using MSDAD.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Linq;

namespace MSDAD
{
    namespace Server
    {
        class Server : MarshalByRefObject, IMSDADServer, IMSDADServerPuppet
        {
            private readonly Dictionary<String, Meeting> Meetings = new Dictionary<string, Meeting>();

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

                Meeting meeting = Meetings[topic];
                if (!meeting.CanJoin(userId))
                {
                    throw new CannotJoinMeetingException("User " + userId + " cannot join this meeting.");
                }

                foreach (Slot slot in meeting.Slots.Intersect(Slot.ParseSlots(slots)))
                {
                    slot.AddUserId(userId);
                }

            }

            String IMSDADServer.ListMeetings(String userId)
            {
                String meetings = "";
                foreach (Meeting meeting in Meetings.Values.Where(x => x.CanJoin(userId)).ToList())
                {
                    
                        meetings += meeting.ToString();
                }
                return meetings;
            }

            void IMSDADServer.CloseMeeting(String topic, String userId)
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

                //FIXME What happens if no room is avaliable
                //FIXME Which Users get to Join the Meeting?
                Slot slot = meeting.Slots.Where(x => x.GetNumUsers() >= meeting.MinParticipants)
                                         .First(x => x.GetAvailableRoom(meeting.MinParticipants) != null);
                
                if (slot == null)
                {
                    throw new NoMeetingAvailableException("No slot meets the requirements. Meeting Canceled");
                }

                slot.GetAvailableRoom(meeting.MinParticipants).AddBooking(slot.Date);
                Meetings.Remove(topic);
                return; 
            }

            void IMSDADServerPuppet.AddRoom(String location, uint capacity, String roomName)
            {
                lock (this)
                {
                    Location local = Location.FromName(location);
                    if (local == null)
                    {
                        local = new Location(location);
                        Location.AddLocation(local);
                    }
                    local.AddRoom(new Room(roomName, capacity));
                }

            }
        }
    }
}
