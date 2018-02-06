using System.Diagnostics;
using Inedo.Diagnostics;

namespace Tests
{
    internal sealed class TestLogger : ILogSink
    {
        public static ILogSink Instance = new TestLogger();

        public void Log(IMessage message)
        {
            Debug.WriteLine(message.Level + " - " + message.Message);
        }
    }
}
