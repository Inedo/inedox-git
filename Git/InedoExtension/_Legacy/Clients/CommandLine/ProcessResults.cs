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

        public int ExitCode { get; }
        public IList<string> Output { get; }
        public IList<string> Error { get; }
    }
}
