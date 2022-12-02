using System;
using Inedo.Agents;

namespace Inedo.Extensions.Clients.LibGitSharp
{
    internal sealed class FileArchiver : LocalFileOperationsExecuter
    {
        private string rootPath;

        public FileArchiver(string rootPath)
        {
            if (rootPath == null)
                throw new ArgumentNullException(nameof(rootPath));

            this.rootPath = rootPath;
        }

        public override string GetBaseWorkingDirectory()
        {
            return this.rootPath;
        }
    }
}
