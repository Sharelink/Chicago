using CSharpServerFramework.Extension;
using CSharpServerFramework;
using CSServerJsonProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Chicago.Extension
{
    [ExtensionInfo("NotificationCenter")]
    public class NotificaionCenterExtension : JsonExtensionBase
    {
        public static NotificaionCenterExtension Instance { get; private set; }
        private IDictionary<string, ICSharpServerSession> subscriptionMap;
        public void Subscript(string userId, ICSharpServerSession session)
        {
            using (var subscription = ChicagoServer.MessagePubSubServerClientManager.GetClient().CreateSubscription())
            {
                subscription.OnUnSubscribe = channel =>
                {
                };

                subscription.OnSubscribe = channel =>
                {
                    subscriptionMap[channel] = session;
                };

                subscription.OnMessage = (channel, message) =>
                {
                    var ss = subscriptionMap[channel];
                    if (ss != null)
                    {
                        if (message == "UnSubscribe")
                        {
                            subscriptionMap.Remove(channel);
                            subscription.UnSubscribeFromChannels(channel);
                        }else if (message.StartsWith( "ChatMessage"))
                        {
                            this.SendJsonResponse(ss, new { ChatId = message.Replace("ChatMessage:","") }, ExtensionName, "UsrNewMsg");
                        }
                        else if (message.StartsWith("LinkMessage"))
                        {
                            this.SendJsonResponse(ss, new { }, ExtensionName, "UsrNewLinkMsg");
                        }else if(message.StartsWith("ShareThingMessage"))
                        {
                            this.SendJsonResponse(ss, new { }, ExtensionName, "UsrNewSTMsg");
                        }
                    }
                };
                subscription.SubscribeToChannels(userId);
            };
        }

        public override void Init()
        {
            subscriptionMap = new Dictionary<string, ICSharpServerSession>();
            ChicagoServer.Instance.OnSessionDisconnected += Instance_OnSessionDisconnected;
            Instance = this;
        }

        private void Instance_OnSessionDisconnected(object sender, CSServerEventArgs e)
        {
            var session = e.State as ICSharpServerSession;
            if (session != null)
            {
                var sharelinker = session.User as Sharelinker;
                if (sharelinker != null)
                {
                    using (var psClient = ChicagoServer.MessagePubSubServerClientManager.GetClient())
                    {
                        var userId = sharelinker.UserData.UserId;
                        
                        psClient.PublishMessage(userId, "UnSubscribe");
                    }
                }
            }
        }

        [CommandInfo(1, "UsrNewMsg")]
        public void NotifyUserNewMessage(ICSharpServerSession session, dynamic msg)
        {
            this.SendJsonResponse(session, new { ChatId = "" }, ExtensionName, "UsrNewMsg");
        }
    }
}
