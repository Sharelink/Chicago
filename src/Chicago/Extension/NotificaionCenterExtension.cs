using CSharpServerFramework.Extension;
using CSharpServerFramework;
using CSServerJsonProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ServiceStack.Redis;
using System.Net.Http;

namespace Chicago.Extension
{
    [ExtensionInfo("NotificationCenter")]
    public class NotificaionCenterExtension : JsonExtensionBase
    {
        public static NotificaionCenterExtension Instance { get; private set; }
        private IDictionary<string, Sharelinker> registUserMap;

        public override void Init()
        {
            registUserMap = new Dictionary<string, Sharelinker>();
            ChicagoServer.Instance.OnSessionDisconnected += OnSessionDisconnected;
            Instance = this;
        }

        private void OnSessionDisconnected(object sender, CSServerEventArgs e)
        {
            var session = e.State as ICSharpServerSession;
            var sharelinker = session.User as Sharelinker;
            if (sharelinker != null)
            {
                sharelinker.IsOnline = false;
            }
        }

        public void Subscript(string userId, ICSharpServerSession session)
        {
            try
            {
                var sharelinker = registUserMap[userId];
                CloseSession(sharelinker.Session);
                registUserMap[userId] = session.User as Sharelinker;
            }
            catch (Exception)
            {
                registUserMap[userId] = session.User as Sharelinker;
                Log(string.Format("Chicago Instance Online Users:{0}", registUserMap.Count));
                SubscribeToPubSubSystem(userId);
            }
            
        }

        private void SubscribeToPubSubSystem(string userId)
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
                };

                subscription.OnMessage = (channel, message) =>
                {
                    var ss = registUserMap[channel];
                    if (ss != null)
                    {
                        if (message == "UnSubscribe")
                        {
                            registUserMap.Remove(channel);
                            subscription.UnSubscribeFromChannels(channel);
                        }
                        else if (ss.IsOnline)
                        {
                            SendChicagoMessageToClient(message, ss);
                        }
                        else
                        {
                            SendAPNs(ss.DeviceToken, message);
                        }
                    }
                };
                subscription.SubscribeToChannels(userId);
            };
        }

        private void SendAPNs(string deviceToken,string message)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
            {
                Console.WriteLine("Device Token is null");
                return;
            }
            Task.Run(async () =>
            {
                string notifyFormat;
                if (message.StartsWith("ChatMessage"))
                {
                    notifyFormat = "NEW_MSG_NOTIFICATION";
                }
                else if (message.StartsWith("LinkMessage"))
                {
                    notifyFormat = "NEW_MSG_NOTIFICATION";
                }
                else
                {
                    return;
                }
                var app_master_secret = "biru3uttfc5nqlfd0aqnm2kxzeivfnle";
                var appkey = "5643e78367e58ec557005b9f";
                var timestamp = BahamutCommon.DateTimeUtil.ConvertDateTimeSecondInt(DateTime.Now);
                var method = "POST";
                var url = "http://msg.umeng.com/api/send";
                var p = new
                {
                    appkey = appkey,
                    timestamp = timestamp,
                    device_tokens = deviceToken,
                    type = "unicast",
                    payload = new
                    {
                        aps = new
                        {
                            alert = new { loc_key = notifyFormat },
                            badge = 1
                        },
                        display_type = "notification"
                    }
                };
                var post_body = Newtonsoft.Json.JsonConvert.SerializeObject(p).Replace("loc_key", "loc-key");
                var md5 = new DBTek.Crypto.MD5_Hsr();
                var sign = md5.HashString(string.Format("{0}{1}{2}{3}", method, url, post_body, app_master_secret)).ToLower();
                var client = new HttpClient();
                var uri = new Uri(string.Format("{0}?sign={1}", url, sign));
                var msg = await client.PostAsync(uri, new StringContent(post_body, System.Text.Encoding.UTF8, "application/json"));
                var result = await msg.Content.ReadAsStringAsync();
                if (msg.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Log("UMeng Message:" + result);
                }
                else
                {
                    Console.WriteLine(result);
                }
            });
        }

        private void SendChicagoMessageToClient(string message, Sharelinker ss)
        {
            Task.Run(() =>
            {
                if (message.StartsWith("ChatMessage"))
                {
                    this.SendJsonResponse(ss.Session, new { ChatId = message.Replace("ChatMessage:", "") }, ExtensionName, "UsrNewMsg");
                }
                else if (message.StartsWith("LinkMessage"))
                {
                    this.SendJsonResponse(ss.Session, new { }, ExtensionName, "UsrNewLinkMsg");
                }
                else if (message.StartsWith("ShareThingMessage"))
                {
                    this.SendJsonResponse(ss.Session, new { }, ExtensionName, "UsrNewSTMsg");
                }
            });
        }

        public bool UnSubscribeUserSession(ICSharpServerSession session)
        {
            if (session != null)
            {
                var sharelinker = session.User as Sharelinker;
                if (sharelinker != null)
                {
                    using (var psClient = ChicagoServer.MessagePubSubServerClientManager.GetClient())
                    {
                        var userId = sharelinker.UserData.UserId;

                        return psClient.PublishMessage(userId, "UnSubscribe") > 0;
                    }
                }
            }
            return false;
        }

        [CommandInfo(1, "UsrNewMsg")]
        public void NotifyUserNewMessage(ICSharpServerSession session, dynamic msg)
        {
            this.SendJsonResponse(session, new { ChatId = "" }, ExtensionName, "UsrNewMsg");
        }

        [CommandInfo(2, "RegistDeviceToken")]
        public void RegistDeviceToken(ICSharpServerSession session, dynamic msg)
        {
            string deviceToken = msg.DeviceToken;
            var sharelinker = session.User as Sharelinker;
            try
            {
                sharelinker = registUserMap[sharelinker.UserData.UserId];
                sharelinker.DeviceToken = deviceToken;
                SendAPNs(deviceToken, "ChatMessage");
            }
            catch (Exception)
            {
                Log(string.Format("Regist Device Token Error:{0}", sharelinker.UserData.UserId));
                throw;
            }
        }
    }
}
