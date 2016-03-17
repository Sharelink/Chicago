using CSharpServerFramework;
using ServiceStack.Redis;
using BahamutService;
using BahamutService.Service;
using System.Collections;
using System.Collections.Generic;

namespace Chicago
{
    public class ChicagoServer : CSServer
    {
        public static TokenService TokenService { get; private set; }
        public static ChicagoServer Instance { get; private set; }
        public static BahamutPubSubService BahamutPubSubService { get; private set; }


        protected override void ServerInit()
        {
            base.ServerInit();
            Instance = this;
            var pbServerUrl = Program.Configuration["Data:MessagePubSubServer:url"].Replace("redis://", "");
            var psClientMgr = new BasicRedisClientManager(pbServerUrl);
            psClientMgr.GetClient().CreateSubscription();
            BahamutPubSubService = new BahamutPubSubService(psClientMgr);

            var tokenServerUrl = Program.Configuration["Data:TokenServer:url"].Replace("redis://", "");
            var tokenServerClientManager = new PooledRedisClientManager(tokenServerUrl);
            TokenService = new TokenService(tokenServerClientManager);
        }

        protected override void AfterStartServerInit()
        {
            base.AfterStartServerInit();
        }

        protected override void ServerDispose()
        {
            BahamutPubSubService = null;
            TokenService = null;
            base.ServerDispose();
        }
    }
}
