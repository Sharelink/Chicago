using CSharpServerFramework.Extension;
using CSharpServerFramework;
using CSServerJsonProtocol;
using System;
using System.Threading.Tasks;
using NLog;
using BahamutService.Service;
using Newtonsoft.Json;

namespace Chicago.Extension
{
    [ExtensionInfo("NotificationCenter")]
    public class NotificaionCenterExtension : JsonExtensionBase
    {
        public static NotificaionCenterExtension Instance { get; private set; }
        private BahamutUserManager userManager;
        public override void Init()
        {
            userManager = new BahamutUserManager();
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
            var user = session.User as BahamutAppUser;
            if (user != null)
            {
                user.IsOnline = false;
            }
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
                if(msgModel.NotifyType == "RegistUserDevice")
                {
                    userManager.RegistDeviceToken(msgModel, this);
                    return;
                }
                var registedUser = userManager.GetUserWithAppUniqueId(appUniqueId, msgModel.ToUser);
                if (registedUser.IsOnline)
                {
                    if (appUniqueId == "Toronto")
                    {
                        this.SendJsonResponse(registedUser.Session, new { }, ExtensionName, msgModel.NotifyType);
                    }
                    else
                    {
                        this.SendJsonResponse(registedUser.Session, new { NotificationType = msgModel.NotifyType, Info = msgModel.Info }, ExtensionName, "BahamutNotify");
                    }
                }
                else
                {
                    string notify = appUniqueId == "Toronto" ? msgModel.Info : msgModel.NotifyType;
                    if (registedUser.IsIOSDevice)
                    {
                        SendBahamutAPNSNotification(appUniqueId, registedUser.DeviceToken, notify);
                    }
                    else if (registedUser.IsAndroidDevice)
                    {
                        SendAndroidMessageToUMessage(appUniqueId, registedUser.DeviceToken, msgModel);
                    }

                }
            }
            catch (Exception)
            {
                LogManager.GetLogger("Info").Info("No User Regist:{0}", msgModel.ToUser);
            }
        }

        private void SendAndroidMessageToUMessage(string appUniqueId, string deviceToken,  BahamutPublishModel model)
        {
            try
            {
                Task.Run(async () =>
                {
                    var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                    var umessageModel = Program.UMessageApps[appUniqueId];
                    await UMengPushNotificationUtil.PushAndroidNotifyToUMessage(deviceToken, umessageModel.AppkeyAndroid, umessageModel.SecretAndroid, umodel);
                });
            }
            catch (Exception)
            {
                LogManager.GetLogger("Info").Info("No App Regist:{0}", appUniqueId);
            }
        }

        private void SendBahamutAPNSNotification(string appUniqueId, string deviceToken, string notifyType)
        {
            try
            {
                var umessageModel = Program.UMessageApps[appUniqueId];
                Task.Run(async () =>
                {
                    await UMengPushNotificationUtil.PushAPNSNotifyToUMessage(deviceToken, notifyType, umessageModel.AppkeyIOS, umessageModel.SecretIOS);
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
            var appUser = session.User as BahamutAppUser;
            string deviceToken = msg.DeviceToken;
            string deviceType = msg.DeviceType;
            userManager.UpdateUserDeviceToken(appUser, deviceToken, deviceType);
        }

        public void RegistUser(string userId, ICSharpServerSession session)
        {
            userManager.RegistUser(userId, session, this);
        }

        public bool RemoveUser(BahamutAppUser user)
        {
            return userManager.RemoveUser(user);
        }
    }
        
}
