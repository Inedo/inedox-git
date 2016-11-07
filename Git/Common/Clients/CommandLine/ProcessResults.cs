using System.Collections.Generic;

namespace Inedo.Extensions.Clients.CommandLine
{
    internal sealed class ProcessResults
    {
        public ProcessResults(int exitCode, IList<string> output, IList<string> error)
        {
            this.ExitCode = exitCode;
            this.Output = output;
            this.Error = error;
        }

        public int ExitCode { get; private set; }
        public IList<string> Output { get; private set; }
        public IList<string> Error { get; private set; }
    }
}
