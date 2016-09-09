using BahamutService.Model;
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
        private static TimeSpan DeviceTokenExpireTime = TimeSpan.FromDays(14);
        
        public void RegistDeviceToken(BahamutPublishModel msgModel)
        {
            dynamic msg = Newtonsoft.Json.JsonConvert.DeserializeObject(msgModel.Info);
            var dt = new DeviceToken
            {
                Token = msg.DeviceToken,
                Type = msg.DeviceType
            };
            ChicagoServer.BahamutPubSubService.RegistUserDevice(msgModel.ToUser, dt, DeviceTokenExpireTime);
        }

        public bool RemoveUser(BahamutPublishModel msgModel)
        {
            return ChicagoServer.BahamutPubSubService.RemoveUserDevice(msgModel.ToUser);
        }

        public void UpdateUserDeviceToken(BahamutAppUser appUser, DeviceToken deviceToken)
        {
            try
            {
                ChicagoServer.BahamutPubSubService.RegistUserDevice(appUser.UserData.UserId, appUser.DeviceToken, DeviceTokenExpireTime);
                appUser.DeviceToken = deviceToken;
            }
            catch (Exception)
            {
                LogManager.GetLogger("Warn").Info("Regist Device Token Error:{0}", appUser.UserData.UserId);
            }

        }

        public static DeviceToken GetUserDeviceToken(string userId)
        {
            try
            {
                return ChicagoServer.BahamutPubSubService.GetUserDeviceToken(userId, DeviceTokenExpireTime);
            }
            catch (Exception)
            {
                LogManager.GetLogger("Warn").Info("Get Device Token Error:{0}", userId);
                return null;
            }
        }

    }
}
