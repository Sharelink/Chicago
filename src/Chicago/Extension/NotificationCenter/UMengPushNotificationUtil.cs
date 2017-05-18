using BahamutCommon.Encryption;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Chicago.Extension
{
    public class UMengMessageModel
    {
        public string Ticker { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public string AfterOpen { get; set; }
        public string Custom { get; set; }
        public int BuilderId { get; set; }
        public string LocKey { get; set; }

        public object Extra { get; set; }

        public string ProductMode { get; set; }
    }

    public class UMengPushNotificationUtil
    {
        public static async Task PushAndroidNotifyToUMessage(string deviceTokens, string appkey, string app_master_secret, UMengMessageModel model)
        {
            var type = deviceTokens.Contains(",") ? "listcast" : "unicast";
            var p = new
            {
                appkey = appkey,
                timestamp = (long)BahamutCommon.DateTimeUtil.UnixTimeSpan.TotalSeconds,
                device_tokens = deviceTokens,
                type = type,
                production_mode = string.IsNullOrWhiteSpace(model.ProductMode) ? "true" : model.ProductMode,
                payload = new
                {
                    body = new
                    {
                        ticker = model.Ticker == null ? "app_name" : model.Ticker,
                        title = model.Title == null ? "new_msg" : model.Title,
                        text = model.Text,
                        after_open = model.AfterOpen,
                        custom = model.Custom,
                        builder_id = model.BuilderId
                    },
                    extra = model.Extra,
                    display_type = "notification"
                }
            };
            await PushNotifyToUMessage(app_master_secret, p);
        }

        public static async Task PushAPNSNotifyToUMessage(string deviceTokens, string appkey, string app_master_secret, UMengMessageModel model)
        {
            var type = deviceTokens.Contains(",") ? "listcast" : "unicast";
            var p = new
            {
                appkey = appkey,
                timestamp = (long)BahamutCommon.DateTimeUtil.UnixTimeSpan.TotalSeconds,
                device_tokens = deviceTokens,
                type = type,
                production_mode = string.IsNullOrWhiteSpace(model.ProductMode) ? "true" : model.ProductMode,
                payload = new
                {
                    aps = new
                    {
                        alert = new { loc_key = model.LocKey },
                        badge = 1,
                        sound = "default",
                        content_available = 1
                    },
                    custom = model.Custom
                }

            };
            await PushNotifyToUMessage(app_master_secret, p);
        }

        private static Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            Formatting = Newtonsoft.Json.Formatting.None
        };

        public static async Task PushNotifyToUMessage(string app_master_secret, object msgParams)
        {
            var method = "POST";
            var url = "http://msg.umeng.com/api/send";
            var post_body = Newtonsoft.Json.JsonConvert.SerializeObject(msgParams, JsonSerializerSettings)
            .Replace("loc_key", "loc-key").Replace("content_available", "content-available");
            var sign = MD5.ComputeMD5Hash(string.Format("{0}{1}{2}{3}", method, url, post_body, app_master_secret));
            var client = new HttpClient();
            var uri = new Uri(string.Format("{0}?sign={1}", url, sign));
            var msg = await client.PostAsync(uri, new StringContent(post_body, System.Text.Encoding.UTF8, "application/json"));
            var result = await msg.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(result) || !result.Contains("SUCCESS"))
            {
                LogManager.GetLogger("Warn").Info("UMSG:{0}", result);
            }
            else
            {
                LogManager.GetLogger("Info").Info("UMSG:{0}", result);
            }

        }
    }
}
