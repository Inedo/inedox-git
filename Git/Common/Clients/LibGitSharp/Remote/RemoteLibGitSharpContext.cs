using Inedo.Serialization;

namespace Inedo.Extensions.Clients.LibGitSharp.Remote
{
    [SlimSerializable]
    internal sealed class RemoteLibGitSharpContext
    {
        public RemoteLibGitSharpContext()
        {
        }

        [SlimSerializable]
        public string WorkingDirectory { get; set; }
        [SlimSerializable]
        public bool Simulation { get; set; }

        [SlimSerializable]
        public string LocalRepositoryPath { get; set; }
        [SlimSerializable]
        public string RemoteRepositoryUrl { get; set; }
        [SlimSerializable]
        public string UserName { get; set; }
        [SlimSerializable]
        public string Password { get; set; }

        [SlimSerializable]
        public string TargetDirectory { get; set; }
        [SlimSerializable]
        public bool KeepInternals { get; set; }
        [SlimSerializable]
        public GitCloneOptions CloneOptions { get; set; }
        [SlimSerializable]
        public string Tag { get; set; }
        [SlimSerializable]
        public string Commit { get; set; }
        [SlimSerializable]
        public string TagMessage { get; set; }
        [SlimSerializable]
        public GitUpdateOptions UpdateOptions { get; set; }
        [SlimSerializable]
        public string FileName { get; set; }
        [SlimSerializable]
        public bool Force { get; set; }
    }
}
