using CSharpServerFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpServerFramework.Client;
using CSharpServerFramework.Extension;
using CSharpServerFramework.Log;
using CSharpServerFramework.Message;
using CSharpServerFramework.Server;
using BahamutCommon;
using ServiceStack.Redis;
using BahamutService;

namespace Chicago
{
    public class ChicagoServer : CSServer
    {
        public static RedisManagerPool MessagePubSubServerClientManager { get; private set; }
        public static TokenService TokenService { get; private set; }

        public static ChicagoServer Instance { get; private set; }

        protected override void ServerInit()
        {
            var key = AppkeyUtil.GenerateAppkey("Chicago");
            Console.WriteLine(key);
            base.ServerInit();
            Instance = this;
        }

        protected override void AfterStartServerInit()
        {
            MessagePubSubServerClientManager = new RedisManagerPool(Program.Configuration["Data:MessagePubSubServer:url"]);
            var tokenServerClientManager = new RedisManagerPool(Program.Configuration["Data:TokenServer:url"]);
            TokenService = new TokenService(tokenServerClientManager);
            base.AfterStartServerInit();
        }

        protected override void ServerDispose()
        {
            MessagePubSubServerClientManager = null;
            TokenService = null;
            base.ServerDispose();
        }
    }
}
