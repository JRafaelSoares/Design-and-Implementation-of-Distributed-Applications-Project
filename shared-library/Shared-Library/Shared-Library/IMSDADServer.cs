using MSDAD.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace MSDAD
{
    namespace Shared
    {

        public interface IMSDADPCS
        {
            void CreateProcess(String type, String args);
        }

        public interface IMSDADServer
        {
            Dictionary<String, String> NewClient(String url, String id);
            HashSet<ServerClient> CreateMeeting(string topic, Meeting meeting);
            void JoinMeeting(String topic, List<string> slots, String userId, DateTime timestamp);
            IDictionary<String, Meeting> ListMeetings(Dictionary<String, Meeting> meetings);
            void CloseMeeting(String topic, String userId);
            String getRandomClient(String clientId);

        }

        [Serializable]
        public class ServerException : ApplicationException
        {
            public String messageError;

            public ServerException(string m)
            {
                this.messageError = m;
            }

            protected ServerException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
                messageError = info.GetString("messageError");
            }

            public String GetErrorMessage()
            {
                return this.messageError;
            }

            [SecurityPermissionAttribute(SecurityAction.Demand,
            SerializationFormatter = true)]
            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
                info.AddValue("messageError", messageError);
            }
        }

        [Serializable]
        public class CannotJoinMeetingException : ServerException
        {
            public CannotJoinMeetingException(String m) : base(m) { }
            public CannotJoinMeetingException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        [Serializable]
        public class CannotCreateMeetingException : ServerException
        {
            public CannotCreateMeetingException(String m) : base(m) { }
            public CannotCreateMeetingException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        [Serializable]
        public class NoSuchMeetingException : ServerException
        {
            public NoSuchMeetingException(String m) : base(m) { }
            public NoSuchMeetingException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }
        [Serializable]
        public class ClientNotCoordenatorException : ServerException
        {
            public ClientNotCoordenatorException(String m) : base(m) { }
            public ClientNotCoordenatorException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        [Serializable]
        public class TopicDoesNotExistException : ServerException
        {
            public TopicDoesNotExistException(String m) : base(m) { }
            public TopicDoesNotExistException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        [Serializable]
        public class LocationDoesNotExistException : ServerException
        {
            public LocationDoesNotExistException(String m) : base(m) { }
            public LocationDoesNotExistException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        [Serializable]
        public class NoMeetingAvailableException : ServerException
        {
            public NoMeetingAvailableException(String m) : base(m) { }
            public NoMeetingAvailableException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        public interface IMSDADServerPuppet
        {
            void AddRoom(String location, uint capacity, String roomName);
            void Status();
            void Crash();
            void Freeze();
            void Unfreeze();
            void ShutDown();
        }

        public interface IMSDADServerToServer
        {
            String NewServer(String id, String url);
            void NewClient(ServerClient client);
            void Ping();
            void CreateMeeting(String topic, Meeting meeting);
            void CloseMeeting(String topic, Meeting meeting);
            void MergeClosedMeeting(String topic, Meeting meeting);
            Meeting LockMeeting(String topic);
            void JoinMeeting(String topic, List<string> slots, String userId, DateTime timestamp);
            void RB_Send(String messageId, String operation, Object[] args);
            void Deliver_CausalOrder(ConcurrentDictionary<String, int> clock, string operation, object[] args);

        }

        public interface IMSDADClientToClient
        {
            void CreateMeeting(String topic, Meeting meeting);
            Dictionary<String, Meeting> SendMeetings();
        }



        public interface IMSDADClientPuppet
        {
            void ShutDown();
        }
    }

    [Serializable]
    public class ServerClient
    {
        public String Url { get; }
        public String ClientId { get; }
        public ServerClient(String url, String clientId)
        {
            this.Url = url;
            this.ClientId = clientId;
        }

        public override bool Equals(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                ServerClient s = (ServerClient)obj;
                return s.ClientId == this.ClientId;
            }
        }

        public override int GetHashCode()
        {
            return this.ClientId.GetHashCode();
        }
    }
}
