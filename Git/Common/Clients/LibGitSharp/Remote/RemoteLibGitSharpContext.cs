using System;

namespace Inedo.Extensions.Clients.LibGitSharp.Remote
{
    [Serializable]
    internal sealed class RemoteLibGitSharpContext
    {
        public RemoteLibGitSharpContext()
        {
        }
        
        public string WorkingDirectory { get; set; }
        public bool Simulation { get; set; }

        public string LocalRepositoryPath { get; set; }
        public string RemoteRepositoryUrl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public string TargetDirectory { get; set; }
        public GitCloneOptions CloneOptions { get; set; }
        public string Tag { get; set; }
        public GitUpdateOptions UpdateOptions { get; set; }
    }
}
