using CSharpServerFramework.Extension;
using CSharpServerFramework;
using CSServerJsonProtocol;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using NLog;
using ServiceStack.Redis;
using BahamutService.Service;

namespace Chicago.Extension
{
    [ExtensionInfo("NotificationCenter")]
    public class NotificaionCenterExtension : JsonExtensionBase
    {
        public static NotificaionCenterExtension Instance { get; private set; }
        private IDictionary<string, BahamutAppUser> registUserMap;

        public override void Init()
        {
            registUserMap = new Dictionary<string, BahamutAppUser>();
            ChicagoServer.Instance.OnSessionDisconnected += OnSessionDisconnected;
            Instance = this;
        }

        private void OnSessionDisconnected(object sender, CSServerEventArgs e)
        {
            var session = e.State as ICSharpServerSession;
            var sharelinker = session.User as BahamutAppUser;
            if (sharelinker != null)
            {
                sharelinker.IsOnline = false;
            }
        }

        public void RegistUser(string userId, ICSharpServerSession session)
        {
            var newUser = session.User as BahamutAppUser;
            var key = GenerateRegistUserMapKey(newUser.UserData.Appkey, userId);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            try
            {   
                var oldClientUser = registUserMap[key];
                CloseSession(oldClientUser.Session);
                registUserMap[key] = newUser;
            }
            catch (Exception)
            {
                registUserMap[key] = newUser;
                LogManager.GetLogger("Info").Info("Chicago Instance Online Users:{0}", registUserMap.Count);
            }
        }

        public bool RemoveUser(BahamutAppUser user)
        {
            var key = GenerateRegistUserMapKey(user.UserData.Appkey, user.UserData.UserId);
            return registUserMap.Remove(key);
        }

        private string GenerateRegistUserMapKey(string appkey,string userId)
        {
            var appUniqueId = ChicagoServer.GetAppUniqueIdByAppkey(appkey);
            if (string.IsNullOrWhiteSpace(appUniqueId) || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }
            return GenerateRegistUserMapKeyByAppUniqueId(appUniqueId, userId);
        }

        private string GenerateRegistUserMapKeyByAppUniqueId(string appUniqueId, string userId)
        {
            return string.Format("{0}:{1}", appUniqueId, userId);
        }

        public void SubscribeToPubSubSystem(string channel)
        {
#if DEBUG
            LogManager.GetLogger("Debug").Debug("Subscribe Channel:" + channel);
#endif
            using (var subscription = ChicagoServer.BahamutPubSubService.CreateSubscription())
            {
                subscription.OnUnSubscribe = appUniqueId =>
                {
                    LogManager.GetLogger("Info").Info("OnUnSubscribe:{0}", appUniqueId);
                };

                subscription.OnSubscribe = appUniqueId =>
                {
                    LogManager.GetLogger("Info").Info("OnSubscribe:{0}", appUniqueId);
                };

                subscription.OnMessage = (appUniqueId, message) =>
                {
                    var msgModel = ChicagoServer.BahamutPubSubService.DeserializePublishMessage(message);
                    try
                    {
                        var ss = registUserMap[GenerateRegistUserMapKeyByAppUniqueId(appUniqueId, msgModel.ToUser)];
                        if (ss.IsOnline)
                        {
                            SendChicagoMessageToClient(msgModel, ss);
                        }
                        else
                        {
                            SendAPNs(ss.DeviceToken, msgModel);
                        }
                    }
                    catch (Exception)
                    {

                    }
                };
                subscription.SubscribeToChannels(channel);
            };
        }

        private void SendAPNs(string deviceToken,BahamutPublishModel message)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
            {
                Console.WriteLine("Device Token is null");
                return;
            }
            Task.Run(async () =>
            {
                string notifyFormat;
                if (message.NotifyType == "ChatMessage")
                {
                    notifyFormat = "NEW_MSG_NOTIFICATION";
                }
                else if (message.NotifyType == "LinkMessage")
                {
                    notifyFormat = "NEW_FRI_MSG_NOTIFICATION";
                }
                else if (message.NotifyType == "ShareThingMessage")
                {
                    notifyFormat = "NEW_SHARE_NOTIFICATION";
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
                            badge = 1,
                            sound = "default"
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
                    LogManager.GetLogger("Info").Info("UMeng Message:" + result);
                }
                else
                {
                    Console.WriteLine(result);
                }
            });
        }

        private void SendChicagoMessageToClient(BahamutPublishModel message, BahamutAppUser ss)
        {
            Task.Run(() =>
            {
                if (message.NotifyType == "ChatMessage")
                {
                    this.SendJsonResponse(ss.Session, new { ChatId = message.Info }, ExtensionName, "UsrNewMsg");
                }
                else if (message.NotifyType == "LinkMessage")
                {
                    this.SendJsonResponse(ss.Session, new { }, ExtensionName, "UsrNewLinkMsg");
                }
                else if (message.NotifyType == "ShareThingMessage")
                {
                    this.SendJsonResponse(ss.Session, new { }, ExtensionName, "UsrNewSTMsg");
                }
            });
        }

        public bool UnSubscribeChannel(string channel)
        {
            try
            {
                ChicagoServer.BahamutPubSubService.UnSubscribe(channel);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
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
            var appUser = session.User as BahamutAppUser;
            try
            {
                appUser = registUserMap[GenerateRegistUserMapKey(appUser.UserData.Appkey, appUser.UserData.UserId)];
                appUser.DeviceToken = deviceToken;
            }
            catch (Exception)
            {
                LogManager.GetLogger("Info").Info("Regist Device Token Error:{0}", appUser.UserData.UserId);
            }
        }
    }
}
