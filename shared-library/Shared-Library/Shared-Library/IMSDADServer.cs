using System;
using System.Collections.Generic;

namespace MSDAD
{
    namespace Shared
    {
        public interface IMSDADServer
        {
            void CreateMeeting(string coordId, string topic, int minParticipants, HashSet<string> slots, HashSet<string> invitees = null);

            void JoinMeeting(String topic, HashSet<string> slots, String userId);

            IList<String> ListMeetings(String userId);

            void CloseMeeting(String topic, String userId);
        }
    }
}
