using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared_Library
{
    public interface IMSDADServer
    {
        void createMeeting(string coordId, string topic, int minParticipants, ISet<string> slots, ISet<string> users = null);

        void joinMeeting(String topic, ISet<string> slots, String userId);

        ISet<String> listMeetings(String userId);

        void closeMeeting(String topic, String userId);
    }
}
