using Inedo.Agents;

namespace Tests
{
    internal sealed class TestFileOperationsExecuter : LocalFileOperationsExecuter
    {
        private string baseWorkingDirectory;

        public TestFileOperationsExecuter(string baseWorkingDirectory)
        {
            this.baseWorkingDirectory = baseWorkingDirectory;
        }

        public override string GetBaseWorkingDirectory() => this.baseWorkingDirectory;
    }
}
