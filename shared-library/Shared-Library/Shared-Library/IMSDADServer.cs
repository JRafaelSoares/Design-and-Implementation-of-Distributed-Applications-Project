using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSDAD
{
    namespace Shared
    {
        public interface IMSDADServer
        {
            void CreateMeeting(string coordId, string topic, int minParticipants, ISet<string> slots, ISet<string> invitees = null);

            void JoinMeeting(String topic, ISet<string> slots, String userId);

            IList<String> ListMeetings(String userId);

            void CloseMeeting(String topic, String userId);
        }
    }
}
