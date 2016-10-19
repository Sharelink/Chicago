using CSharpServerFramework.Extension;
using CSharpServerFramework;
using CSServerJsonProtocol;
using System;
using System.Threading.Tasks;
using NLog;
using BahamutService.Service;
using Newtonsoft.Json;
using BahamutService.Model;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        private IDictionary<string, UMessageAppModel> UMessageApps { get; set; }

        public override void Init()
        {
            userManager = new BahamutUserManager();
            ChicagoServer.Instance.OnSessionDisconnected += OnSessionDisconnected;
            Instance = this;
            LoadNotifyApps();
        }

        private void LoadNotifyApps()
        {
            UMessageApps = new Dictionary<string, UMessageAppModel>();
            var apps = Program.Configuration.GetSection("NotifyApps").GetChildren();
            foreach (var app in apps)
            {
                string appkey = app["appkey"];
                string channel = Program.GetAppChannelByAppkey(appkey);
                if (string.IsNullOrWhiteSpace(channel))
                {
                    LogManager.GetLogger("Warning").Warn("App Channel Not Found Of Appkey:{0}", appkey);
                    continue;
                }
                UMessageApps.Add(channel, new UMessageAppModel
                {
                    AppkeyIOS = app["umessage:ios:appkey"],
                    SecretIOS = app["umessage:ios:secret"],

                    AppkeyAndroid = app["umessage:android:appkey"],
                    SecretAndroid = app["umessage:android:secret"]
                });
                SubscribeToPubSubSystem(channel);
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

        public async void SubscribeToPubSubSystem(string channel)
        {
            await ChicagoServer.BahamutPubSubService.SubscribeAsync(channel, (chel, value) =>
            {
                HandleSubscriptionMessage(chel, value);
            });
        }

        private void HandleSubscriptionMessage(string channel, string message)
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
                        HandleNotificationMessageAsync(channel, msgModel);
                    }
                }
                catch (Exception)
                {
                    LogManager.GetLogger("Warn").Warn("App={0}:Handle Subscription Error:{1}", channel, message);
                }
            });
        }

        private async void HandleNotificationMessageAsync(string channel, BahamutPublishModel msgModel)
        {
            string iosTokens = null;
            string androidTokens = null;
            if (msgModel.ToUser.Contains(","))
            {
                var iosTokensBuilder = new StringBuilder();
                var androidTokensBuilder = new StringBuilder();
                var userIds = msgModel.ToUser.Split(new char[]{','});
                var deviceTokens = await ChicagoServer.BahamutPubSubService.GetUserDeviceTokensAsync(userIds);
                foreach (var dt in deviceTokens)
                {
                    if (dt.IsIOSDevice())
                    {
                        if(iosTokensBuilder.Length > 0)
                        {
                            iosTokensBuilder.Append(',');
                        }
                        iosTokensBuilder.Append(dt.Token);
                    }
                    else if (dt.IsAndroidDevice())
                    {
                        if (androidTokensBuilder.Length > 0)
                        {
                            androidTokensBuilder.Append(',');
                        }
                        androidTokensBuilder.Append(dt.Token);
                    }
                }
                iosTokens = iosTokensBuilder.ToString();
                androidTokens = androidTokensBuilder.ToString();
            }
            else
            {
                var deviceToken = await BahamutUserManager.GetUserDeviceTokenAsync(msgModel.ToUser);
                if (deviceToken != null)
                {
                    if (deviceToken.IsIOSDevice())
                    {
                        iosTokens = deviceToken.Token;
                    }
                    else if (deviceToken.IsAndroidDevice())
                    {
                        androidTokens = deviceToken.Token;
                    }
                }
                else
                {
                    LogManager.GetLogger("Warn").Warn("App={0}:User Not Regist DeviceToken:{1}", channel, msgModel.ToUser);
                }
            }
            
            if(!string.IsNullOrWhiteSpace(iosTokens))
            {
                SendBahamutAPNSNotification(channel, iosTokens, msgModel);
            }

            if(!string.IsNullOrWhiteSpace(androidTokens))
            {
                SendAndroidMessageToUMessage(channel, androidTokens, msgModel);
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

        private void SendAndroidMessageToUMessage(string appChannel, string deviceToken, BahamutPublishModel model)
        {
            Task.Run(() =>
            {
                try
                {
                    var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                    var umessageModel = UMessageApps[appChannel];
                    UMengPushNotificationUtil.PushAndroidNotifyToUMessage(deviceToken, umessageModel.AppkeyAndroid, umessageModel.SecretAndroid, umodel);
                }
                catch (Exception)
                {
                    LogManager.GetLogger("Warn").Warn("No App Regist:{0}", appChannel);
                }
            });
        }

        private void SendBahamutAPNSNotification(string appChannel, string deviceToken, BahamutPublishModel model)
        {
            Task.Run(() =>
            {
                try
                {
                    var umessageModel = UMessageApps[appChannel];
                    var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                    UMengPushNotificationUtil.PushAPNSNotifyToUMessage(deviceToken, umessageModel.AppkeyIOS, umessageModel.SecretIOS, umodel);
                }
                catch (Exception)
                {
                    LogManager.GetLogger("Warn").Warn("No App Regist:{0}", appChannel);
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
