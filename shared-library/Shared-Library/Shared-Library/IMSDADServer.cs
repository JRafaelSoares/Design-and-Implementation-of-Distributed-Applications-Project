using System;
using System.Collections.Generic;

namespace MSDAD
{
    namespace Shared
    {
        public interface IMSDADServer
        {
            void CreateMeeting(string coordId, string topic, uint minParticipants, List<String> slots, HashSet<string> invitees = null);

            void JoinMeeting(String topic, List<string> slots, String userId);

            String ListMeetings(String userId);

            String CloseMeeting(String topic, String userId);
        }

        public class ServerException : ApplicationException
        {
            public String messageError;

            public ServerException(string m)
            {
                this.messageError = m;
            }

            public String getErrorMessage()
            {
                return this.messageError;
            }
        }

        [Serializable]
        public class CannotJoinMeetingException : ServerException
        {
            public CannotJoinMeetingException(string m) : base(m) { }
        }

        [Serializable]
        public class ClientNotCoordenatorException : ServerException
        {
            public ClientNotCoordenatorException(string m) : base(m) { }
        }

        [Serializable]
        public class TopicDoesNotExistException : ServerException
        {
            public TopicDoesNotExistException(string m) : base(m) { }
        }

        [Serializable]
        public class NoMeetingAvailableException : ServerException
        {
            public NoMeetingAvailableException(string m) : base(m) { }
        }

        public interface IMSDADServerPuppet
        {
            void addRoom(String location, uint capacity, String roomName);

        }
    }
}

