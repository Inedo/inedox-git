using System;

namespace Inedo.Extensions.Clients
{
    [Serializable]
    public sealed class GitUpdateOptions
    {
        public GitUpdateOptions()
        {
        }

        public string Tag { get; set; }
        public string Branch { get; set; }
        public bool RecurseSubmodules { get; set; }
    }
}
