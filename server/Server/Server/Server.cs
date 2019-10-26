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
                lock (this)
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
            }

            void IMSDADServer.JoinMeeting(String topic, List<String> slots, String userId)
            {
                lock (this)
                {
                    Meeting m = Meetings[topic];
                    List<Slot> MeetingSlots = m.Slots;
                    List<Slot> ClientSlots = Slot.ParseSlots(slots);

                    foreach (Slot cslot in ClientSlots)
                    {
                        foreach (Slot mslot in MeetingSlots)
                        {
                            if (cslot.Equals(mslot))
                            {
                                mslot.addUserId(userId);
                                break;
                            }
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

            void IMSDADServer.CloseMeeting(String topic, String userId)
            {
                lock (this)
                {
                    Meeting meeting = Meetings[topic];

                    throw new NotImplementedException();
                }
            }

            void IMSDADServerPuppet.addRoom(String location, uint capacity, String roomName)
            {
                lock (this) {
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
