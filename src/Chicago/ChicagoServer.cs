using CSharpServerFramework;
using BahamutService;
using BahamutService.Service;
using System.Collections;
using System.Collections.Generic;
using DataLevelDefines;

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
            var psClientMgr = DBClientManagerBuilder.GenerateRedisConnectionMultiplexer(Program.Configuration.GetSection("Data:MessagePubSubServer"));
            BahamutPubSubService = new BahamutPubSubService(psClientMgr);

            var tokenServerClientManager = DBClientManagerBuilder.GenerateRedisConnectionMultiplexer(Program.Configuration.GetSection("Data:TokenServer"));
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
