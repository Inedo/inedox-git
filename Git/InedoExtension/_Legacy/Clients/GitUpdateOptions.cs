using Inedo.Serialization;

namespace Inedo.Extensions.Clients
{
    [SlimSerializable]
    internal sealed class GitUpdateOptions
    {
        [SlimSerializable]
        public string Ref { get; set; }
        [SlimSerializable]
        public string Branch { get; set; }
        [SlimSerializable]
        public bool RecurseSubmodules { get; set; }
    }
}
