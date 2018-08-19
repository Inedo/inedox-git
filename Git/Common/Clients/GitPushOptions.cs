using System;

namespace Inedo.Extensions.Clients
{
    [Serializable]
    public sealed class GitPushOptions
    {
        public GitPushOptions()
        {
        }

        public string Ref { get; set; }
        public bool Force { get; set; }
    }
}
