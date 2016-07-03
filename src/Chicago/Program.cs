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
        public static IDictionary<string, string> NotifyApps { get; private set; }
        public static IDictionary<string, UMessageAppModel> UMessageApps { get; private set; }
        private static void LoadNotifyApps()
        {
            UMessageApps = new Dictionary<string, UMessageAppModel>();
            NotifyApps = new Dictionary<string, string>();
            var apps = Program.Configuration.GetSection("NotifyApps").GetChildren();
            foreach (var app in apps)
            {
                string key = app["appkey"];
                string value = app["uniqueId"];
                NotifyApps.Add(key, value);
                UMessageApps.Add(value, new UMessageAppModel
                {
                    AppkeyIOS = app["umessage:ios:appkey"],
                    SecretIOS = app["umessage:ios:secret"],

                    AppkeyAndroid = app["umessage:android:appkey"],
                    SecretAndroid = app["umessage:android:secret"]
                });
            }
        }

        public static string GetAppUniqueIdByAppkey(string appkey)
        {

            try
            {
                var id = NotifyApps[appkey];
                return id;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

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
                conBuilder.AddJsonFile("notify_apps_debug.json",true,true);
                Console.WriteLine("Debug Mode");
#else
                conBuilder.AddJsonFile("/etc/bahamut/chicago_notify_apps.json",true,true);
                configFile = "/etc/bahamut/chicago.json";
#endif
            }

            conBuilder.AddJsonFile(configFile);
            Configuration = conBuilder.Build();

            //Nlog
            var nlogConfig = new NLog.Config.LoggingConfiguration();
            BahamutCommon.LoggerLoaderHelper.LoadLoggerToLoggingConfig(nlogConfig, Configuration, "Data:Log:fileLoggers");

#if DEBUG
            BahamutCommon.LoggerLoaderHelper.AddConsoleLoggerToLogginConfig(nlogConfig);
#endif
            NLog.LogManager.Configuration = nlogConfig;

            //Notify Apps
            LoadNotifyApps();

            try
            {
                //CSServer
                var server = new ChicagoServer();
                Server = server;
                server.UseNetConfig(new NetConfigReader());
                server.UseServerConfig(new ServerConfigReader());
                server.UseLogger(new FileLogger(Configuration["Data:ServerLog"]));
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
                NLog.LogManager.GetLogger("Chicago").Fatal(ex);
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
