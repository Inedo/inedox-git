using System;

namespace Inedo.Extensions.Clients
{
    [Serializable]
    public sealed class GitUpdateOptions
    {
        public GitUpdateOptions()
        {
        }

        public string Ref { get; set; }
        public string Branch { get; set; }
        public bool RecurseSubmodules { get; set; }
        public bool IsBare { get; set; }
    }
}
