using System;

namespace Inedo.Extensions.GitHub.Clients
{
    [Serializable]
    public class Reference
    {
        public string Name { get; }
        public string Type { get; }
        public string Hash { get; }

        internal Reference(string name, string type, string hash)
        {
            this.Name = name;
            this.Type = type;
            this.Hash = hash;
        }
    }
}