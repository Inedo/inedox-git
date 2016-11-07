using System;
using System.Diagnostics;
using Inedo.Diagnostics;

namespace Tests
{
    internal sealed class TestLogger : ILogger
    {
        public static ILogger Instance = new TestLogger();

        event EventHandler<LogMessageEventArgs> ILogger.MessageLogged
        {
            add { }
            remove { }
        }

        void ILogger.Log(MessageLevel logLevel, string message)
        {
            Debug.WriteLine(logLevel + " - " + message);
        }
    }
}
