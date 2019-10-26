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

        [Serializable]
        public class CannotJoinMeetingException : ApplicationException
        {
            public String messageError;

            public CannotJoinMeetingException(string m)
            {
                this.messageError = m;
            }

            public String getErrorMessage()
            {
                return this.messageError;
            }

        }

        [Serializable]
        public class ClientNotCoordenatorException : ApplicationException
        {
            public String messageError;

            public ClientNotCoordenatorException(string m)
            {
                this.messageError = m;
            }

            public String getErrorMessage()
            {
                return this.messageError;
            }

        }

        [Serializable]
        public class TopicDoesNotExistException : ApplicationException
        {
            public String messageError;

            public TopicDoesNotExistException(string m)
            {
                this.messageError = m;
            }

            public String getErrorMessage()
            {
                return this.messageError;
            }

        }

        [Serializable]
        public class NoMeetingAvailableException : ApplicationException
        {
            public String messageError;

            public NoMeetingAvailableException(string m)
            {
                this.messageError = m;
            }

            public String getErrorMessage()
            {
                return this.messageError;
            }

        }

        public interface IMSDADServerPuppet
        {
            void addRoom(String location, uint capacity, String roomName);

        }
    }
}

