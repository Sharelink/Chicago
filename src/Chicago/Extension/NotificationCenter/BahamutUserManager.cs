using BahamutService.Service;
using CSharpServerFramework;
using CSServerJsonProtocol;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Chicago.Extension
{
    class BahamutUserManager
    {
        private IDictionary<string, BahamutAppUser> registUserMap;
        public BahamutUserManager()
        {
            registUserMap = new ConcurrentDictionary<string, BahamutAppUser>();
        }

        public void RegistUser(string userId, ICSharpServerSession session, NotificaionCenterExtension extension)
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
                    extension.SendJsonResponse(oldClientUser.Session, new { }, "BahamutUserValidation", "OtherDeviceLogin");
                }
                registUserMap[key] = newUser;
            }
            catch (Exception)
            {
                registUserMap[key] = newUser;
                LogManager.GetLogger("Info").Info("Chicago Instance Online Users:{0}", registUserMap.Count);
            }
        }

        public void RegistDeviceToken(BahamutPublishModel msgModel, JsonExtensionBase extension)
        {
            dynamic msg = Newtonsoft.Json.JsonConvert.DeserializeObject(msgModel.Info);
            var user = new BahamutAppUser();
            user.UserData = new BahamutService.Model.AccountSessionData
            {
                AccountId = msg.AccountId,
                Appkey = msg.Appkey,
                AppToken = msg.AppToken,
                UserId = msgModel.ToUser
            };
            user.DeviceToken = msg.DeviceToken;
            user.DeviceType = msg.DeviceType;
            user.IsOnline = false;

            var key = GenerateRegistUserMapKey(user.UserData.Appkey, user.UserData.UserId);
            try
            {
                var originUser = registUserMap[key];
                if (originUser.IsOnline)
                {
                    extension.SendJsonResponse(originUser.Session, new { }, "BahamutUserValidation", "OtherDeviceLogin");
                }
                if (originUser.DeviceToken != user.DeviceToken)
                {
                    registUserMap[key] = user;
                }
            }
            catch (Exception)
            {
                registUserMap[key] = user;
            }
        }

        public bool RemoveUser(BahamutPublishModel msgModel)
        {
            dynamic msg = Newtonsoft.Json.JsonConvert.DeserializeObject(msgModel.Info);
            var key = GenerateRegistUserMapKey(msg.Appkey, msg.ToUser);
            return registUserMap.Remove(key);
        }

        public void UpdateUserDeviceToken(BahamutAppUser appUser, string deviceToken, string deviceType)
        {
            try
            {
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

        public bool RemoveUser(BahamutAppUser user)
        {
            var key = GenerateRegistUserMapKey(user.UserData.Appkey, user.UserData.UserId);
            return registUserMap.Remove(key);
        }

        private string GenerateRegistUserMapKey(string appkey, string userId)
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

        public BahamutAppUser GetUserWithAppUniqueId(string appUniqueId, string userId)
        {
            return registUserMap[GenerateRegistUserMapKeyByAppUniqueId(appUniqueId, userId)];
        }

        public BahamutAppUser GetUserWithAppKey(string appkey, string userId)
        {
            return registUserMap[GenerateRegistUserMapKey(appkey, userId)];
        }
    }
}
