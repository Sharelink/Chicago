using CSharpServerFramework.Log;
using NLog.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Chicago
{
    public class FileLogger : ILoggerLog
    {
        public FileLogger(string path)
        {
            LoggingConfiguration config = new LoggingConfiguration();
            var fileTaget = new NLog.Targets.FileTarget();
            fileTaget.FileName = path;
            fileTaget.Name = "FileLogger";
            fileTaget.Layout = "${message}";
            var logRule = new LoggingRule("*", NLog.LogLevel.Debug, fileTaget);
            config.AddTarget(fileTaget);
            config.LoggingRules.Add(logRule);
            NLog.LogManager.Configuration = config;
        }
        public void Log(string LogString)
        {
            NLog.LogManager.GetLogger("Chicago").Info(LogString);
        }

        public void Close()
        {
        }
    }
}
