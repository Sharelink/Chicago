using CSharpClientFramework;
using CSharpClientFramework.Client;
using CSServerJsonProtocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace ChicagoClientLib
{
    public class ChicagoClient : CSharpServerClientBase
    {
        public ChicagoClient() : base(new JsonMessageDeserializer())
        {
            
        }

        public void Validate(string appkey,string appInstanceId)
        {
            if (IsRunning)
            {
                SendJsonMessage("BahamutAppValidation", "Login", new
                {
                    Appkey = appkey,
                    AppInstanceId = appInstanceId
                });
            }
            else
            {
                throw new CSharpClientException("Client Not Running,Invoke Start Before Validate");
            }
        }

        public void AddValidateReturnHandler(EventHandler<CSharpServerClientEventArgs> Callback)
        {
            AddHandlerCallback("BahamutAppValidation", "Login", Callback);
        }

        public void SendJsonMessageAsync(string Extension, int CommandId, object Message)
        {
            var msgBytes = JsonProtocolUtil.SerializeMessage(Extension, CommandId, Message);
            SendMessageAsync(msgBytes, msgBytes.Length);
        }

        public void SendJsonMessage(string Extension, int CommandId, object Message)
        {
            var msgBytes = JsonProtocolUtil.SerializeMessage(Extension, CommandId, Message);
            SendMessage(msgBytes, msgBytes.Length);
        }

        public void SendJsonMessageAsync(string Extension, string CommandName, object Message)
        {
            var msgBytes = JsonProtocolUtil.SerializeMessage(Extension, CommandName, Message);
            SendMessageAsync(msgBytes, msgBytes.Length);
        }

        public void SendJsonMessage(string Extension,string CommandName,object Message)
        {
            var msgBytes = JsonProtocolUtil.SerializeMessage(Extension, CommandName, Message);
            SendMessage(msgBytes, msgBytes.Length);
        }

        public void Start(IPAddress iPAddress, object chicagoServerPort)
        {
            throw new NotImplementedException();
        }
    }

    class JsonMessageDeserializer : IDeserializeMessage
    {
        public CSharpServerClientBaseMessage GetMessageFromBuffer(byte[] Buffer, int len)
        {
            var route = JsonProtocolUtil.DeserializeRoute(Buffer, len);
            var returnObj = JsonProtocolUtil.DeserializeMessage(0, Buffer, len);
            var jsonMsg = new JsonMessage()
            {
                CommandId = route.CmdId,
                Extension = route.ExtName,
                CommandName = route.CmdName,
                Result = returnObj
            };
            return jsonMsg;
        }
    }


    [Serializable]
    public class CSharpClientException : Exception
    {
        public CSharpClientException() { }
        public CSharpClientException(string message) : base(message) { }
        public CSharpClientException(string message, Exception inner) : base(message, inner) { }
        protected CSharpClientException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }

    public class JsonMessage : CSharpServerClientBaseMessage
    {
        public dynamic Result { get; set; }
    }
}
