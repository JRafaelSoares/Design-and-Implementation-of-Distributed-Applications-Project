using System;
using System.Collections.Generic;

namespace MSDAD
{
    namespace Shared
    {
        public interface IMSDADServer
        {
            void CreateMeeting(string coordId, string topic, uint minParticipants, ISet<string> slots, ISet<string> invitees = null);

            void JoinMeeting(String topic, ISet<string> slots, String userId);

            IList<String> ListMeetings(String userId);

            void CloseMeeting(String topic, String userId);
        }
    }
}
