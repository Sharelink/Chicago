using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Chicago.Extension
{
    public class UMengPushNotificationUtil
    {

        public static async Task PushAndroidNotifyToUMessage(string deviceToken, string notifyFormat, string appkey, string app_master_secret)
        {
            var p = new
            {
                appkey = appkey,
                timestamp = BahamutCommon.DateTimeUtil.ConvertDateTimeSecondInt(DateTime.Now),
                device_tokens = deviceToken,
                type = "unicast",
                payload = new
                {
                    body = new
                    {
                        custom = notifyFormat
                    },
                    display_type = "notification"
                }
            };
            await PushNotifyToUMessage(deviceToken, notifyFormat, app_master_secret, appkey, p);
        }

        public static async Task PushAPNSNotifyToUMessage(string deviceToken, string notifyFormat, string appkey, string app_master_secret)
        {
            var p = new
            {
                appkey = appkey,
                timestamp = BahamutCommon.DateTimeUtil.ConvertDateTimeSecondInt(DateTime.Now),
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
            await PushNotifyToUMessage(deviceToken, notifyFormat, app_master_secret, appkey, p);
        }

        private static async Task PushNotifyToUMessage(string deviceToken, string notifyFormat, string app_master_secret, string appkey,object msgParams)
        {
            var method = "POST";
            var url = "http://msg.umeng.com/api/send";
            var post_body = Newtonsoft.Json.JsonConvert.SerializeObject(msgParams).Replace("loc_key", "loc-key");
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
