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
            var dt = new DeviceToken
            {
                Token = msg.DeviceToken,
                Type = msg.DeviceType
            };
            return await ChicagoServer.BahamutPubSubService.RegistUserDeviceAsync(msgModel.ToUser, dt, DeviceTokenExpireTime);
        }

        public async Task<bool> RemoveUserAsync(BahamutPublishModel msgModel)
        {
            return await ChicagoServer.BahamutPubSubService.RemoveUserDeviceAsync(msgModel.ToUser);
        }

        public async Task<bool> UpdateUserDeviceTokenAynce(BahamutAppUser appUser, DeviceToken deviceToken)
        {
            try
            {
                var suc = await ChicagoServer.BahamutPubSubService.RegistUserDeviceAsync(appUser.UserData.UserId, appUser.DeviceToken, DeviceTokenExpireTime);
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

        public static async Task<DeviceToken> GetUserDeviceTokenAsync(string userId)
        {
            try
            {
                var tokenExpiry = await ChicagoServer.BahamutPubSubService.GetUserDeviceTokenWithExpiryAsync(userId);
                if (tokenExpiry != null)
                {
                    if (tokenExpiry.Item2.TotalSeconds > 0)
                    {
                        if (tokenExpiry.Item2.TotalSeconds < DeviceTokenExpireTime.TotalSeconds * 0.1)
                        {
                            await ChicagoServer.BahamutPubSubService.ExpireUserDeviceTokenAsync(userId, DeviceTokenExpireTime);
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

        public static async Task<IEnumerable<DeviceToken>> GetUsersDeviceTokensAsync(IEnumerable<string> userIds)
        {
            return await ChicagoServer.BahamutPubSubService.GetUserDeviceTokensAsync(userIds);
        }
    }
}
