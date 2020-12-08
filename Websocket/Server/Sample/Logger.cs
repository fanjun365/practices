using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class Logger : ILogger
    {
        public bool LogEvents { get; set; }
        public Logger()
        {
            LogEvents = true;
        }

        public void Info(string msg)
        {
            if (LogEvents) Console.WriteLine(msg);
        }

        public void Warn(string msg)
        {
            if (LogEvents) Console.WriteLine(msg);
        }

        public void Error(string msg, Exception ex)
        {
            if (LogEvents) Console.WriteLine($"{msg}\r\n{ex.ToString()}");
        }
    }
}
