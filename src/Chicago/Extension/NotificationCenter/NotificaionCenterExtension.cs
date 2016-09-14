using CSharpServerFramework.Extension;
using CSharpServerFramework;
using CSServerJsonProtocol;
using System;
using System.Threading.Tasks;
using NLog;
using BahamutService.Service;
using Newtonsoft.Json;
using BahamutService.Model;

namespace Chicago.Extension
{
    [ExtensionInfo("NotificationCenter")]
    public class NotificaionCenterExtension : JsonExtensionBase
    {
        private readonly string DEFAULT_NOTIFY_CMD = "BahamutNotify";
        private readonly string NOTIFY_TYPE_REGIST_USER_DEVICE_TOKEN = "RegistUserDevice";
        private readonly string NOTIFY_TYPE_REMOVE_USER_DEVICE_TOKEN = "RemoveUserDevice";

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
            var sub = ChicagoServer.BahamutPubSubService.CreateSubscription();
            sub.SubscribeAsync(channel, (chel, value) =>
            {
                HandleSubscriptionMessage(chel, value);
            });
            #region ServiceStack
            /*
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
            */
            #endregion
        }

        private void HandleSubscriptionMessage(string appUniqueId, string message)
        {
            Task.Run(async () =>
            {
                try
                {
                    var msgModel = ChicagoServer.BahamutPubSubService.DeserializePublishMessage(message);
                    if (msgModel.NotifyType == NOTIFY_TYPE_REGIST_USER_DEVICE_TOKEN)
                    {
                        await userManager.RegistDeviceTokenAsync(msgModel);
                    }
                    else if (msgModel.NotifyType == NOTIFY_TYPE_REMOVE_USER_DEVICE_TOKEN)
                    {
                        await userManager.RemoveUserAsync(msgModel);
                    }
                    else
                    {
                        HandleNotificationMessageAsync(appUniqueId, msgModel);
                    }
                }
                catch (Exception)
                {
                    LogManager.GetLogger("Warn").Warn("App={0}:Handle Subscription Error:{1}", appUniqueId, message);
                }
            });
        }

        private async void HandleNotificationMessageAsync(string appUniqueId, BahamutPublishModel msgModel)
        {
            var deviceToken = await BahamutUserManager.GetUserDeviceTokenAsync(msgModel.ToUser);
            if (deviceToken != null && deviceToken.IsValidToken())
            {
                if (deviceToken.IsIOSDevice())
                {
                    SendBahamutAPNSNotification(appUniqueId, deviceToken.Token, msgModel);
                }
                else if (deviceToken.IsAndroidDevice())
                {
                    SendAndroidMessageToUMessage(appUniqueId, deviceToken.Token, msgModel);
                }
            }
            else
            {
                LogManager.GetLogger("Info").Warn("App={0}:User Not Regist DeviceToken:{1}", appUniqueId, msgModel.ToUser);
            }
        }

        private void SendBahamutNotifyCmd(BahamutPublishModel msgModel, BahamutAppUser registedUser)
        {
            var cmd = string.IsNullOrWhiteSpace(msgModel.CustomCmd) ? DEFAULT_NOTIFY_CMD : msgModel.CustomCmd;
            object resObj = null;
            if (msgModel.NotifyType == null && msgModel.Info == null)
            {
                resObj = new { };
            }
            else if (msgModel.Info == null)
            {
                resObj = new { NotificationType = msgModel.NotifyType };
            }
            else if (msgModel.NotifyType == null)
            {
                resObj = new { Info = msgModel.Info };
            }
            else
            {
                resObj = new { NotificationType = msgModel.NotifyType, Info = msgModel.Info };
            }
            this.SendJsonResponse(registedUser.Session, resObj, ExtensionName, cmd);
        }

        private void SendAndroidMessageToUMessage(string appUniqueId, string deviceToken, BahamutPublishModel model)
        {
            Task.Run(() =>
            {
                try
                {
                    var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                    var umessageModel = Program.UMessageApps[appUniqueId];
                    UMengPushNotificationUtil.PushAndroidNotifyToUMessage(deviceToken, umessageModel.AppkeyAndroid, umessageModel.SecretAndroid, umodel);
                }
                catch (Exception)
                {
                    LogManager.GetLogger("Info").Info("No App Regist:{0}", appUniqueId);
                }
            });
        }

        private void SendBahamutAPNSNotification(string appUniqueId, string deviceToken, BahamutPublishModel model)
        {
            Task.Run(() =>
            {
                try
                {
                    var umessageModel = Program.UMessageApps[appUniqueId];
                    var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                    UMengPushNotificationUtil.PushAPNSNotifyToUMessage(deviceToken, umessageModel.AppkeyIOS, umessageModel.SecretIOS, umodel);
                }
                catch (Exception)
                {
                    LogManager.GetLogger("Info").Info("No App Regist:{0}", appUniqueId);
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

        [CommandInfo(1, "RegistDeviceToken")]
        public void RegistDeviceToken(ICSharpServerSession session, dynamic msg)
        {
            var appUser = session.User as BahamutAppUser;

            var dt = new DeviceToken
            {
                Token = msg.DeviceToken,
                Type = msg.DeviceType
            };
            Task.Run(async () =>
            {
                await userManager.UpdateUserDeviceTokenAynce(appUser, dt);
            });
        }

        public void RegistUser(string userId, ICSharpServerSession session)
        {
            //userManager.RegistUser(userId, session, this);
        }

        public bool RemoveUser(BahamutAppUser user)
        {
            //return userManager.RemoveUser(user);
            return false;
        }
    }
        
}
