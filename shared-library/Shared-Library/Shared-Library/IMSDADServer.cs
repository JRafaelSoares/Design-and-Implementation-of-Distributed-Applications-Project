using System;
using System.Collections.Generic;

namespace MSDAD
{
    namespace Shared
    {
        public interface IMSDADServer
        {
            void CreateMeeting(string coordId, string topic, int minParticipants, List<String> slots, HashSet<string> invitees = null);

            void JoinMeeting(String topic, List<string> slots, String userId);

            IList<String> ListMeetings(String userId);

            void CloseMeeting(String topic, String userId);
        }
    }
}
