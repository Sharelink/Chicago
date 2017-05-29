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

        public async Task<bool> RegistDeviceTokenAsync(BahamutPublishModel msgModel)
        {
            dynamic msg = Newtonsoft.Json.JsonConvert.DeserializeObject(msgModel.Info);
            var appkey = msgModel.Appkey;
            var dt = new DeviceToken
            {
                Token = msg.DeviceToken,
                Type = msg.DeviceType
            };
            string accountid = msg.AccountId;
            LogManager.GetLogger("Info").Info("Regist {0} Device Of Account:{1}", dt.Type, accountid);
            return await ChicagoServer.BahamutPubSubService.RegistUserDeviceAsync(appkey, msgModel.ToUser, dt, DeviceTokenExpireTime);
        }

        public async Task<bool> RemoveUserAsync(BahamutPublishModel msgModel)
        {
            return await ChicagoServer.BahamutPubSubService.RemoveUserDeviceAsync(msgModel.Appkey, msgModel.ToUser);
        }

        public async Task<bool> UpdateUserDeviceTokenAynce(string appkey, BahamutAppUser appUser, DeviceToken deviceToken)
        {
            try
            {
                var suc = await ChicagoServer.BahamutPubSubService.RegistUserDeviceAsync(appkey, appUser.UserData.UserId, appUser.DeviceToken, DeviceTokenExpireTime);
                if (suc)
                {
                    appUser.DeviceToken = deviceToken;
                }
                return suc;
            }
            catch (Exception)
            {
                LogManager.GetLogger("Warn").Info("Regist Device Token Error:{0}", appUser.UserData.UserId);
                return false;
            }

        }

        public static async Task<DeviceToken> GetUserDeviceTokenAsync(string appkey, string userId)
        {
            try
            {
                var tokenExpiry = await ChicagoServer.BahamutPubSubService.GetUserDeviceTokenWithExpiryAsync(appkey, userId);
                if (tokenExpiry != null)
                {
                    if (tokenExpiry.Item2.TotalSeconds > 0)
                    {
                        if (tokenExpiry.Item2.TotalSeconds < DeviceTokenExpireTime.TotalSeconds * 0.1)
                        {
                            await ChicagoServer.BahamutPubSubService.ExpireUserDeviceTokenAsync(appkey, userId, DeviceTokenExpireTime);
                        }
                        return tokenExpiry.Item1;
                    }
                }
            }
            catch (Exception)
            {
            }
            LogManager.GetLogger("Warn").Info("Get Device Token Error:{0}", userId);
            return null;
        }
    }
}
