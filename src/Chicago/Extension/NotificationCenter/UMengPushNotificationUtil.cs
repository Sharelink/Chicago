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
    }

    public class UMengPushNotificationUtil
    {
        public static void PushAndroidNotifyToUMessage(string deviceTokens, string appkey, string app_master_secret, UMengMessageModel model)
        {
            var type = deviceTokens.Contains(",") ? "listcast":"unicast";
            var p = new
            {
                appkey = appkey,
                timestamp = (long)BahamutCommon.DateTimeUtil.UnixTimeSpan.TotalSeconds,
                device_tokens = deviceTokens,
                type = type,
				#if DEBUG
					production_mode = "false",
				#endif
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
                    display_type = "notification"
                }
            };
            PushNotifyToUMessage(app_master_secret, p);
        }

        public static void PushAPNSNotifyToUMessage(string deviceTokens, string appkey, string app_master_secret, UMengMessageModel model)
        {
            var type = deviceTokens.Contains(",") ? "listcast":"unicast";
            var p = new
            {
                appkey = appkey,
                timestamp = (long)BahamutCommon.DateTimeUtil.UnixTimeSpan.TotalSeconds,
                device_tokens = deviceTokens,
                type = type,
				#if DEBUG
					production_mode = "false",
				#endif
                payload = new
                {
                    aps = new
                    {
                        alert = new { loc_key = model.LocKey },
                        badge = 1,
                        sound = "default"
                    },
					custom = model.Custom
                }
				
            };
            PushNotifyToUMessage(app_master_secret, p);
        }

        public static void PushNotifyToUMessage(string app_master_secret, object msgParams)
        {
            Task.Run(async () =>
            {
                var method = "POST";
                var url = "http://msg.umeng.com/api/send";
                var post_body = Newtonsoft.Json.JsonConvert.SerializeObject(msgParams).Replace("loc_key", "loc-key");
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
            });
            
        }
    }
}
