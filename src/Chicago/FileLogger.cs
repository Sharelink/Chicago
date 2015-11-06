using CSharpServerFramework.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Chicago
{
    public class FileLogger : ILoggerLog
    {
        public StreamWriter LogFileWriter { get; private set; }
        public FileLogger(string path)
        {
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            LogFileWriter = new StreamWriter(stream);
            LogFileWriter.AutoFlush = true;
        }
        public void Log(string LogString)
        {
            LogFileWriter.WriteLine(LogString);
        }

        public void Close()
        {
            LogFileWriter.Flush();
            LogFileWriter.Close();
        }
    }
}
