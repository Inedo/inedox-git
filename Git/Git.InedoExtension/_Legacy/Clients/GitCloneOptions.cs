using Inedo.Serialization;

namespace Inedo.Extensions.Clients
{
    [SlimSerializable]
    internal sealed class GitCloneOptions
    {
        [SlimSerializable]
        public string Branch { get; set; }
        [SlimSerializable]
        public bool RecurseSubmodules { get; set; }

        public override string ToString() => $"Branch={this.Branch ?? "(default)"}; RecurseSubmodules={this.RecurseSubmodules}";
    }
}
