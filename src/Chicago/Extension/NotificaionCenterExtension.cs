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
            foreach (var app in Program.NotifyApps)
            {
                SubscribeToPubSubSystem(app.Value);
            }

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
                if (oldClientUser.IsOnline)
                {
                    this.SendJsonResponse(oldClientUser.Session, new { }, "BahamutUserValidation", "OtherDeviceLogin");
                }
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
            var appUniqueId = Program.GetAppUniqueIdByAppkey(appkey);
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

        private void SubscribeToPubSubSystem(string channel)
        {
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
                    HandleSubscriptionMessage(appUniqueId, message);
                };
                Task.Run(() =>
                {
                    subscription.SubscribeToChannels(channel);
                });
            };
        }

        private void HandleSubscriptionMessage(string appUniqueId, string message)
        {
            var msgModel = ChicagoServer.BahamutPubSubService.DeserializePublishMessage(message);
            try
            {
                var ss = registUserMap[GenerateRegistUserMapKeyByAppUniqueId(appUniqueId, msgModel.ToUser)];
                if (ss.IsOnline)
                {
                    if (appUniqueId == "Toronto")
                    {
                        this.SendJsonResponse(ss.Session, new { }, ExtensionName, msgModel.NotifyType);
                    }
                    else
                    {
                        this.SendJsonResponse(ss.Session, new { NotificationType = msgModel.NotifyType, Info = msgModel.Info }, ExtensionName, "BahamutNotify");
                    }
                }
                else
                {
                    if (ss.IsIOSDevice)
                    {
                        SendBahamutAPNSNotification(appUniqueId, ss.DeviceToken, msgModel);
                    }
                }
            }
            catch (Exception)
            {
                LogManager.GetLogger("Info").Info("No User Regist:{0}", msgModel.ToUser);
            }
        }

        private void SendBahamutAPNSNotification(string appUniqueId, string deviceToken, BahamutPublishModel msgModel)
        {
            try
            {
                var umessageModel = Program.UMessageApps[appUniqueId];
                Task.Run(async () =>
                {
                    await UMengPushNotificationUtil.PushAPNSNotifyToUMessage(deviceToken, msgModel.Info, umessageModel.Secret, umessageModel.Appkey);
                });
            }
            catch (Exception)
            {
                LogManager.GetLogger("Info").Info("No App Regist:{0}", appUniqueId);
            }

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

        [CommandInfo(1, "RegistDeviceToken")]
        public void RegistDeviceToken(ICSharpServerSession session, dynamic msg)
        {
            string deviceToken = msg.DeviceToken;
            string deviceType = msg.DeviceType;
            var appUser = session.User as BahamutAppUser;
            try
            {
                appUser = registUserMap[GenerateRegistUserMapKey(appUser.UserData.Appkey, appUser.UserData.UserId)];
                appUser.DeviceToken = deviceToken;
                if (string.IsNullOrWhiteSpace(deviceType))
                {
                    appUser.DeviceToken = BahamutAppUser.DeviceTypeIOS;
                }
                else
                {
                    appUser.DeviceType = deviceType;
                }
            }
            catch (Exception)
            {
                LogManager.GetLogger("Info").Info("Regist Device Token Error:{0}", appUser.UserData.UserId);
            }
        }
    }

    class UMengPushNotificationUtil
    {
        public static async Task PushAPNSNotifyToUMessage(string deviceToken, string notifyFormat, string app_master_secret, string appkey)
        {
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
        }
    }
}
