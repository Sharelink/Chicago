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

namespace Chicago
{
    public class Program
    {
        public static IConfiguration Configuration { get; private set; }
        public Program(IApplicationEnvironment appEnv)
        {
            var conBuilder = new ConfigurationBuilder();
            conBuilder
                .SetBasePath(appEnv.ApplicationBasePath)
                .AddJsonFile("config.json")
                .AddEnvironmentVariables();
            Configuration = conBuilder.Build();

            
        }

        public void Main(string[] args)
        {
            var server = new ChicagoServer();
            server.UseNetConfig(new NetConfigReader());
            server.UseServerConfig(new ServerConfigReader());
            server.UseLogger(ConsoleLogger.Instance);
            server.UseMessageRoute(new JsonRouteFilter());
            server.UseExtension(new SharelinkerValidateExtension());
            server.UseExtension(new BahamutAppValidateExtension());
            server.UseExtension(new NotificaionCenterExtension());
            server.UseExtension(new HeartBeatExtension());
            server.StartServer();
            while (true)
            {
                var line = Console.ReadLine();
                if (line == "exit")
                {
                    break;  
                }
            }
        }
    }

    class ServerConfigReader : IGetServerConfig
    {
        public uint GetBufferAddPerTimeCount()
        {
            return 2048;
        }

        public uint GetBufferInitCount()
        {
            return 1024;
        }

        public int GetBufferSize()
        {
            return 8 * 1024;
        }

        public int GetNetTimeOut()
        {
            return 15 * 1000;
        }

        public uint GetValidateTimeout()
        {
            //7 mins
            return 1000 * 60 * 7;
        }

        public int GetWorkerThreadCount()
        {
            return 2;
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
