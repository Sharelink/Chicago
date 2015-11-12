﻿using CSharpServerFramework.Extension;
using CSharpServerFramework;
using CSServerJsonProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ServiceStack.Redis;

namespace Chicago.Extension
{
    [ExtensionInfo("NotificationCenter")]
    public class NotificaionCenterExtension : JsonExtensionBase
    {
        public static NotificaionCenterExtension Instance { get; private set; }
        private IDictionary<string, ICSharpServerSession> subscriptionMap;

        public override void Init()
        {
            subscriptionMap = new Dictionary<string, ICSharpServerSession>();
            ChicagoServer.Instance.OnSessionDisconnected += Instance_OnSessionDisconnected;
            Instance = this;
        }

        public void Subscript(string userId, ICSharpServerSession session)
        {
            using (var client = ChicagoServer.MessagePubSubServerClientManager.GetClient())
            using (var subscription = client.CreateSubscription())
            {
                subscription.OnUnSubscribe = channel =>
                {
                    Log(string.Format("OnUnSubscribe User:{0}", channel));
                };

                subscription.OnSubscribe = channel =>
                {
                    Log(string.Format("OnSubscribe User:{0}", channel));
                    subscriptionMap[channel] = session;

                    Log(string.Format("Chicago Instance Online Users:{0}", subscriptionMap.Count));
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
