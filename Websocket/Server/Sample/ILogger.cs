using System;

namespace ConsoleApp1
{
    public interface ILogger
    {
        bool LogEvents { get; set; }

        void Info(string msg);

        void Warn(string msg);

        void Error(string msg, Exception ex);
    }
}