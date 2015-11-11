using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using CSharpServerFramework.Extension;
using System.Net;
using CSharpServerFramework.Server;
using Chicago.Extension;
using CSharpServerFramework.Log;
using CSServerJsonProtocol;
using System.Threading;
using CSharpServerFramework;
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.DependencyInjection;
using Microsoft.AspNet.Builder;

namespace Chicago
{
    public class Program
    {
        public static IConfiguration Configuration { get; private set; }
        public static ChicagoServer Server { get; private set; }
        public Program(IApplicationEnvironment appEnv)
        {
            var conBuilder = new ConfigurationBuilder();
            conBuilder.SetBasePath(appEnv.ApplicationBasePath).AddEnvironmentVariables();
#if DEBUG
            conBuilder.AddJsonFile("config_debug.json");
#else
            conBuilder.AddJsonFile("config.json");
#endif
            Configuration = conBuilder.Build();
            
        }

        public void Main(string[] args)
        {
            var server = new ChicagoServer();
            Server = server;
            server.UseNetConfig(new NetConfigReader());
            server.UseServerConfig(new ServerConfigReader());
#if DEBUG
                server.UseLogger(ConsoleLogger.Instance);
                server.UseLogger(new FileLogger(Configuration["Data:Log:logFile"]));
#else
                server.UseLogger(new FileLogger(Configuration["Data:Log:logFile"]));
#endif
            server.UseMessageRoute(new JsonRouteFilter());
            server.UseExtension(new SharelinkerValidateExtension());
            server.UseExtension(new BahamutAppValidateExtension());
            server.UseExtension(new NotificaionCenterExtension());
            server.UseExtension(new HeartBeatExtension());
            try
            {
                server.StartServer();
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                NLog.LogManager.GetCurrentClassLogger().Fatal(ex);
                throw;
            }
        }
    }

    class ServerConfigReader : IGetServerConfig
    {
        public uint GetBufferAddPerTimeCount()
        {
            return uint.Parse(Program.Configuration["Data:NetConfig:addBufferCountPerTime"]);
        }

        public uint GetBufferInitCount()
        {
            return uint.Parse(Program.Configuration["Data:NetConfig:bufferInitCount"]);
        }

        public int GetBufferSize()
        {
            return int.Parse(Program.Configuration["Data:NetConfig:bufferSize"]);
        }

        public int GetNetTimeOut()
        {
            return int.Parse(Program.Configuration["Data:NetConfig:clientTimeOut"]);
        }

        public uint GetValidateTimeout()
        {
            return uint.Parse(Program.Configuration["Data:NetConfig:validateTimeOut"]);
        }

        public int GetWorkerThreadCount()
        {
            return int.Parse(Program.Configuration["Data:NetConfig:workerThread"]);
        }
    }

    class NetConfigReader : IGetNetConfig
    {
        public int GetListenPort()
        {
            return int.Parse(Program.Configuration["Data:ServerConfig:port"]);
        }

        public int GetMaxListenConnection()
        {
            return int.Parse(Program.Configuration["Data:ServerConfig:maxConnection"]);
        }

        public IPAddress GetServerBindIP()
        {
            var host = Program.Configuration["Data:ServerConfig:host"];
            try
            {
                return IPAddress.Parse(host);
            }
            catch (Exception)
            {
                try
                {
                    var ip = Dns.GetHostEntry(host).AddressList.First();
                    return ip;
                }
                catch (Exception)
                {
                    throw;
                }
                
            }
            
        }
    }
}
