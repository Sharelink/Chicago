using System;
using System.Linq;
using System.Net;
using Chicago.Extension;
using CSharpServerFramework.Log;
using CSServerJsonProtocol;
using System.Threading;
using CSharpServerFramework;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using NLog;

namespace Chicago
{
    public class UMessageAppModel
    {
        public string AppkeyIOS { get; set; }
        public string SecretIOS { get; set; }

        public string AppkeyAndroid { get; set; }
        public string SecretAndroid { get; set; }

    }

    public class Program
    {
        public static IConfiguration Configuration { get; private set; }
        public static ChicagoServer Server { get; private set; }
        
        public static string GetAppChannelByAppkey(string appkey)
        {
            return Configuration[string.Format("AppChannel:{0}:channel", appkey)];
        }

        public static void Main(string[] args)
        {
            var baseConfigBuilder = new ConfigurationBuilder();
            baseConfigBuilder.SetBasePath(Directory.GetCurrentDirectory());
            baseConfigBuilder.AddCommandLine(args);
            baseConfigBuilder.AddEnvironmentVariables();
            var configFile = baseConfigBuilder.Build()["config"];
            if (string.IsNullOrEmpty(configFile))
            {
                Console.WriteLine("No Config File");
                Console.WriteLine("Need Parameter \"--config path\"");
                return;
            }
            baseConfigBuilder.AddJsonFile(configFile, true, true);
            var baseConfig = baseConfigBuilder.Build();
            var logConfigFile = baseConfig["Data:LogConfig"];
            var notifyAppsConfigFile = baseConfig["Data:NotifyAppsConfig"];
            var appChannelConfigFile = baseConfig["Data:AppChannelConfig"];
            baseConfigBuilder
                .AddJsonFile(logConfigFile, true, true)
                .AddJsonFile(notifyAppsConfigFile, true, true)
                .AddJsonFile(appChannelConfigFile, true, true);
            Configuration = baseConfigBuilder.Build();

            //Nlog
            var nlogConfig = new NLog.Config.LoggingConfiguration();
            BahamutCommon.LoggerLoaderHelper.LoadLoggerToLoggingConfig(nlogConfig, Configuration, "Logger:fileLoggers");

#if DEBUG
            BahamutCommon.LoggerLoaderHelper.AddConsoleLoggerToLogginConfig(nlogConfig);
#endif
            LogManager.Configuration = nlogConfig;
            
            try
            {
                //CSServer
                var server = new ChicagoServer();
                Server = server;
                server.UseNetConfig(new NetConfigReader());
                server.UseServerConfig(new ServerConfigReader());
                server.UseLogger(new FileLogger(Configuration["ServerLog"]));
#if DEBUG
                server.UseLogger(ConsoleLogger.Instance);
#endif
                server.UseMessageRoute(new JsonRouteFilter());
                server.UseExtension(new BahamutUserValidationExtension());
                server.UseExtension(new SharelinkerValidateExtension());
                server.UseExtension(new BahamutAppValidateExtension());

                //NotificationCenter Extension
                var notificationExt = new NotificaionCenterExtension();
                server.UseExtension(notificationExt);
                server.UseExtension(new HeartBeatExtension());
                server.StartServer();
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                LogManager.GetLogger("Chicago").Fatal(ex);
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
                return IPAddress.Loopback;
            }
            
        }
    }
}
