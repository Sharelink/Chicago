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
                Task.Run(async ()=>{
                    await HandleSubscriptionMessage(chel, value);
                });
            });
        }

        private async Task HandleSubscriptionMessage(string channel, string message)
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
                    await HandleNotificationMessageAsync(channel, msgModel);
                }
            }
            catch (Exception)
            {
                LogManager.GetLogger("Warn").Warn("App={0}:Handle Subscription Error:{1}", channel, message);
            }
        }

        private async Task HandleNotificationMessageAsync(string channel, BahamutPublishModel msgModel)
        {
            TypedDeviceTokens token = null;
            if (msgModel.ToUser.Contains(","))
            {
                var userIds = msgModel.ToUser.Split(new char[] { ',' });
                token = await GetMultiUserDeviceTokens(userIds);
            }
            else
            {
                token = await GetSingleUserDeviceToken(msgModel.ToUser);
            }

            if (token == null)
            {
                LogManager.GetLogger("Warn").Warn("App={0}:User Not Regist DeviceToken:{1}", channel, msgModel.ToUser);
            }else
            {
                if (!string.IsNullOrWhiteSpace(token.iOsDeviceTokens))
                {
                    await SendBahamutAPNSNotification(channel, token.iOsDeviceTokens, msgModel);
                }

                if (!string.IsNullOrWhiteSpace(token.iOsDeviceTokens))
                {
                    await SendAndroidMessageToUMessage(channel, token.iOsDeviceTokens, msgModel);
                }
            }

        }

        class TypedDeviceTokens
        {
            public string AndroidDeviceTokens { get; set; }
            public string iOsDeviceTokens { get; set; }
        }

        private async Task<TypedDeviceTokens> GetSingleUserDeviceToken(string toUser)
        {
            var result = new TypedDeviceTokens();
            var deviceToken = await BahamutUserManager.GetUserDeviceTokenAsync(toUser);
            if (deviceToken != null)
            {
                if (deviceToken.IsIOSDevice())
                {
                    result.iOsDeviceTokens = deviceToken.Token;
                }
                else if (deviceToken.IsAndroidDevice())
                {
                    result.AndroidDeviceTokens = deviceToken.Token;
                }else
                {
                    return null;
                }
                return result;
            }else
            {
                return null;
            }
        }

        private async Task<TypedDeviceTokens> GetMultiUserDeviceTokens(string[] userIds)
        {
            var result = new TypedDeviceTokens();
            try
            {
                var iosTokensBuilder = new StringBuilder();
                var androidTokensBuilder = new StringBuilder();
                var deviceTokens = await ChicagoServer.BahamutPubSubService.GetUserDeviceTokensAsync(userIds);
                var iosSeparator = "";
                var andSeparator = "";
                LogManager.GetLogger("Info").Info("Mutil User Notification:{0}", deviceTokens.Count());
                foreach (var dt in deviceTokens)
                {
                    if (dt == null || dt.IsUnValidToken())
                    {
                        LogManager.GetLogger("Info").Info("UnvalidToken Type Notification");
                    }
                    else if (dt.IsIOSDevice())
                    {
                        iosTokensBuilder.Append(iosSeparator);
                        iosTokensBuilder.Append(dt.Token);
                        iosSeparator = ",";
                    }
                    else if (dt.IsAndroidDevice())
                    {
                        androidTokensBuilder.Append(andSeparator);
                        androidTokensBuilder.Append(dt.Token);
                        andSeparator = ",";
                    }
                    else
                    {
                        LogManager.GetLogger("Info").Info("Unsupport Type Notification:{0}", dt.Token);
                    }
                }
                result.iOsDeviceTokens = iosTokensBuilder.ToString();
                result.AndroidDeviceTokens = androidTokensBuilder.ToString();
            }
            catch (System.Exception e)
            {
                LogManager.GetLogger("Warn").Warn("Mutil Notification Exception:{0}", e.ToString());
                return null;
            }
            if(string.IsNullOrWhiteSpace(result.AndroidDeviceTokens) && string.IsNullOrWhiteSpace(result.iOsDeviceTokens))
            {
                return null;
            }
            return result;
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

        private async Task SendAndroidMessageToUMessage(string appChannel, string deviceToken, BahamutPublishModel model)
        {
            try
            {
                var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                var umessageModel = UMessageApps[appChannel];
                await UMengPushNotificationUtil.PushAndroidNotifyToUMessage(deviceToken, umessageModel.AppkeyAndroid, umessageModel.SecretAndroid, umodel);
            }
            catch (Exception)
            {
                LogManager.GetLogger("Warn").Warn("No App Regist:{0}", appChannel);
            }
        }

        private async Task SendBahamutAPNSNotification(string appChannel, string deviceToken, BahamutPublishModel model)
        {
            try
            {
                var umessageModel = UMessageApps[appChannel];
                var umodel = JsonConvert.DeserializeObject<UMengMessageModel>(model.NotifyInfo);
                await UMengPushNotificationUtil.PushAPNSNotifyToUMessage(deviceToken, umessageModel.AppkeyIOS, umessageModel.SecretIOS, umodel);
            }
            catch (Exception)
            {
                LogManager.GetLogger("Warn").Warn("No App Regist:{0}", appChannel);
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
