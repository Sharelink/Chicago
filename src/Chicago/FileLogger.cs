using CSharpServerFramework.Log;
using NLog;
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
            var config = LogManager.Configuration == null ? new LoggingConfiguration() : LogManager.Configuration;
            var fileTaget = new NLog.Targets.FileTarget();
            fileTaget.FileName = path;
            fileTaget.Name = "Chicago";
            fileTaget.Layout = "${date:format=yyyy-MM-dd HH\\:mm\\:ss} ${message};${exception}";
            var logRule = new LoggingRule("Chicago", NLog.LogLevel.Debug, fileTaget);
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
