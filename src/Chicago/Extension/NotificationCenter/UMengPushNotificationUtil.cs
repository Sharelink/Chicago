﻿using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public static async Task PushAndroidNotifyToUMessage(string deviceToken, string appkey, string app_master_secret, UMengMessageModel model)
        {
            var p = new
            {
                appkey = appkey,
                timestamp = BahamutCommon.DateTimeUtil.ConvertDateTimeSecondInt(DateTime.Now),
                device_tokens = deviceToken,
                type = "unicast",
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
            await PushNotifyToUMessage(deviceToken, app_master_secret, p);
        }

        public static async Task PushAPNSNotifyToUMessage(string deviceToken, string appkey, string app_master_secret, UMengMessageModel model)
        {
            var p = new
            {
                appkey = appkey,
                timestamp = BahamutCommon.DateTimeUtil.ConvertDateTimeSecondInt(DateTime.Now),
                device_tokens = deviceToken,
                type = "unicast",
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
            await PushNotifyToUMessage(deviceToken, app_master_secret, p);
        }

        private static async Task PushNotifyToUMessage(string deviceToken, string app_master_secret,object msgParams)
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
