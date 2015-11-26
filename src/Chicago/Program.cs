using System;
using System.Linq;
using System.Net;
using Chicago.Extension;
using CSharpServerFramework.Log;
using CSServerJsonProtocol;
using System.Threading;
using CSharpServerFramework;
using Microsoft.Extensions.Configuration;

namespace Chicago
{
    public class Program
    {
        public static IConfiguration Configuration { get; private set; }
        public static ChicagoServer Server { get; private set; }

        public static void Main(string[] args)
        {
            var conBuilder = new ConfigurationBuilder();
            conBuilder.AddEnvironmentVariables();
            string configFile = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--config")
                {
                    try
                    {
                        configFile = args[i + 1];
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("--config no config file path");
                        throw;
                    }
                    
                }
            }

            if (string.IsNullOrWhiteSpace(configFile))
            {
#if DEBUG
                configFile = "config_debug.json";
                Console.WriteLine("Debug Mode");
#else
                configFile = "/etc/bahamut/chicago.json";
#endif
            }

            conBuilder.AddJsonFile(configFile);
            Configuration = conBuilder.Build();
            var server = new ChicagoServer();
            Server = server;
            server.UseNetConfig(new NetConfigReader());
            server.UseServerConfig(new ServerConfigReader());
            server.UseLogger(new FileLogger(Configuration["Data:Log:logFile"]));
#if DEBUG
            server.UseLogger(ConsoleLogger.Instance);
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
