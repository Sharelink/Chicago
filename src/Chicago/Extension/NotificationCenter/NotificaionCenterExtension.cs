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
        private readonly string DEFAULT_NOTIFY_CMD = "BahamutNotify";
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
            try
            {
                var msgModel = ChicagoServer.BahamutPubSubService.DeserializePublishMessage(message);
                if (msgModel.NotifyType == "RegistUserDevice")
                {
                    userManager.RegistDeviceToken(msgModel, this);
                    return;
                }else if(msgModel.NotifyType == "RemoveUserDevice")
                {
                    userManager.RemoveUser(msgModel);
                    return;
                }
                var registedUser = userManager.GetUserWithAppUniqueId(appUniqueId, msgModel.ToUser);
				if (registedUser == null)
				{
					LogManager.GetLogger("Warn").Warn("App={0}:No User:{1}", appUniqueId, msgModel.ToUser);
				}else if (registedUser.IsOnline)
                {
                    var cmd = string.IsNullOrWhiteSpace(msgModel.CustomCmd) ? DEFAULT_NOTIFY_CMD : msgModel.CustomCmd;
                    object resObj = null;
                    if(msgModel.NotifyType == null && msgModel.Info == null)
                    {
                        resObj = new { };
                    }
                    else if (msgModel.Info == null)
                    {
                        resObj = new { NotificationType = msgModel.NotifyType };
                    }else if( msgModel.NotifyType == null)
                    {
                        resObj = new { Info = msgModel.Info };
                    }
                    else
                    {
                        resObj = new { NotificationType = msgModel.NotifyType, Info = msgModel.Info };
                    }
                    this.SendJsonResponse(registedUser.Session, resObj, ExtensionName, cmd);
                }
                else
                {
                    if (registedUser.IsIOSDevice)
                    {
                        SendBahamutAPNSNotification(appUniqueId, registedUser.DeviceToken, msgModel);
                    }
                    else if (registedUser.IsAndroidDevice)
                    {
                        SendAndroidMessageToUMessage(appUniqueId, registedUser.DeviceToken, msgModel);
                    }

                }
            }
            catch (Exception)
            {
                LogManager.GetLogger("Warn").Warn("App={0}:Handle Subscription Error:{1}", appUniqueId, message);
            }
        }

        private void SendAndroidMessageToUMessage(string appUniqueId, string deviceToken,  BahamutPublishModel model)
        {
            try
            {
                Task.Run(() =>
                {
                    var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                    var umessageModel = Program.UMessageApps[appUniqueId];
                    UMengPushNotificationUtil.PushAndroidNotifyToUMessage(deviceToken, umessageModel.AppkeyAndroid, umessageModel.SecretAndroid, umodel);
                });
            }
            catch (Exception)
            {
                LogManager.GetLogger("Info").Info("No App Regist:{0}", appUniqueId);
            }
        }

        private void SendBahamutAPNSNotification(string appUniqueId, string deviceToken, BahamutPublishModel model)
        {
            try
            {
                Task.Run(() =>
                {
					var umessageModel = Program.UMessageApps[appUniqueId];
					var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                    UMengPushNotificationUtil.PushAPNSNotifyToUMessage(deviceToken, umessageModel.AppkeyIOS, umessageModel.SecretIOS,umodel);
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
