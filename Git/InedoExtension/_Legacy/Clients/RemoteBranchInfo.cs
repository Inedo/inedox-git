using Inedo.Serialization;

namespace Inedo.Extensions.Clients
{
    [SlimSerializable]
    internal sealed class RemoteBranchInfo : IEquatable<RemoteBranchInfo>
    {
        public RemoteBranchInfo()
        {
        }
        public RemoteBranchInfo(string name, string commitHash)
        {
            this.Name = name;
            this.CommitHash = commitHash;
        }

        [SlimSerializable]
        public string Name { get; set; }
        [SlimSerializable]
        public string CommitHash { get; set; }

        public bool Equals(RemoteBranchInfo other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return string.Equals(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }
        public override bool Equals(object obj) => this.Equals(obj as RemoteBranchInfo);
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name ?? string.Empty);
    }
}
