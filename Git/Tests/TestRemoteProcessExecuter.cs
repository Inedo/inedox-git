using System.Threading.Tasks;
using Inedo.Agents;

namespace Tests
{
    internal sealed class TestRemoteProcessExecuter : IRemoteProcessExecuter
    {
        public IRemoteProcess CreateProcess(RemoteProcessStartInfo startInfo) => new LocalProcess(startInfo);
        public Task<string> GetEnvironmentVariableValueAsync(string name) => Task.FromResult("");
    }
}
