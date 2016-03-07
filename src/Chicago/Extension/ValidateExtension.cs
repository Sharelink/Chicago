using CSharpServerFramework.Client;
using CSharpServerFramework.Extension;
using CSharpServerFramework.Extension.ServerBaseExtensions;
using CSharpServerFramework;
using CSServerJsonProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BahamutService.Model;
using NLog;

namespace Chicago.Extension
{
    [ValidateExtension]
    [ExtensionInfo("BahamutAppValidation")]
    public class BahamutAppValidateExtension : ExtensionBaseEx
    {
        public BahamutAppValidateExtension():
            base(JsonMessageDeserializer.Instance)
        {

        }

        [CommandInfo(1,"Login")]
        public void Login(ICSharpServerSession session,dynamic msg)
        {
            string appkey = msg.Appkey;
            string appInstanceId = msg.AppInstanceId;
            var response = new
            {
                IsValidate = true
            };
            session.RegistUser(new ChicagoUser());
            this.SendJsonResponse(session, response, ExtensionName, "Login");
        }

        [CommandInfo(2,"Logout")]
        public void Logout(ICSharpServerSession session, dynamic msg)
        {

        }

        public override void Init()
        {
        }
    }

    [ValidateExtension]
    [ExtensionInfo("BahamutUserValidation")]
    public class SharelinkerValidateExtension : ExtensionBaseEx
    {
        public SharelinkerValidateExtension() :
            base(JsonMessageDeserializer.Instance)
        {

        }

        [CommandInfo(1, "Login")]
        public void Login(ICSharpServerSession session, dynamic msg)
        {
            string appToken = msg.AppToken;
            string appkey = msg.Appkey;
            string userId = msg.UserId;
            Task.Run(async () =>
            {
                var result = await ChicagoServer.TokenService.ValidateAppToken(appkey, userId, appToken);
                if (result != null)
                {
                    var sharelinker = new BahamutAppUser()
                    {
                        Session = session,
                        UserData = result,
                        IsOnline = true
                    };
                    session.RegistUser(sharelinker);
                    this.SendJsonResponse(session, new { IsValidate = "true" }, ExtensionName, "Login");
                    LogManager.GetLogger("Info").Info("Login Success:{0}", userId);
                    NotificaionCenterExtension.Instance.RegistUser(result.UserId, session);
                }
                else
                {
                    LogManager.GetLogger("Info").Info("Login Failed:{0}", userId);
                    this.SendJsonResponse(session, new { IsValidate = "false" }, ExtensionName, "Login");
                }
            });
        }

        [CommandInfo(2, "Logout")]
        public void Logout(ICSharpServerSession session, dynamic msg)
        {
            string appToken = msg.AppToken;
            string appkey = msg.Appkey;
            string userId = msg.UserId;
            var user = session.User as BahamutAppUser;
            if (NotificaionCenterExtension.Instance.RemoveUser(user))
            {
                LogManager.GetLogger("Info").Info("Logout Success:{0}", userId);
                this.CloseSession(session);
            }
            else
            {
                LogManager.GetLogger("Info").Info("Logout Failed:{0}", userId);
            }
        }

        public override void Init()
        {
        }
    }

    public class BahamutAppUser : ICSharpServerUser
    {
        public ICSharpServerSession Session { get; set; }
        public AccountSessionData UserData { get; set; }
        public string DeviceToken { get; set; }
        public bool IsOnline { get; set; }
        public bool IsUserValidate
        {
            get
            {
                return true;
            }
        }
    }

    public class ChicagoUser : ICSharpServerUser
    {
        public ICSharpServerSession Session { get; set; }
        public bool IsUserValidate
        {
            get
            {
                return true;
            }
        }

    }
}
