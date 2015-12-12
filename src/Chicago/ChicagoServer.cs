using CSharpServerFramework;
using ServiceStack.Redis;
using BahamutService;

namespace Chicago
{
    public class ChicagoServer : CSServer
    {
        public static IRedisClientsManager MessagePubSubServerClientManager { get; private set; }
        public static TokenService TokenService { get; private set; }
        public static ChicagoServer Instance { get; private set; }

        protected override void ServerInit()
        {
            base.ServerInit();
            Instance = this;
            var pbServerUrl = Program.Configuration["Data:MessagePubSubServer:url"].Replace("redis://", "");
            MessagePubSubServerClientManager = new BasicRedisClientManager(pbServerUrl);

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
            MessagePubSubServerClientManager = null;
            TokenService = null;
            base.ServerDispose();
        }
    }
}
