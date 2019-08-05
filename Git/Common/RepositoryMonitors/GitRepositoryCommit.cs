using System;
using System.Collections.Generic;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.RepositoryMonitors;
using Inedo.Serialization;

namespace Inedo.Extensions.Git.RepositoryMonitors
{
    [Serializable]
    internal sealed class GitRepositoryCommit : RepositoryCommit
    {
        [Persistent]
        [ScriptAlias("CommitHash")]
        public string Hash { get; set; }

        public override bool Equals(RepositoryCommit other)
        {
            if (!(other is GitRepositoryCommit gitCommit))
                return false;

            return string.Equals(this.Hash, gitCommit.Hash, StringComparison.OrdinalIgnoreCase);
        }
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.Hash ?? string.Empty);

        public override string GetFriendlyDescription() => this.ToString();

        public override string ToString()
        {
            if (this.Hash?.Length > 8)
                return this.Hash.Substring(0, 8);
            else
                return this.Hash ?? string.Empty;
        }

        public override IReadOnlyDictionary<RuntimeVariableName, RuntimeValue> GetRuntimeVariables()
        {
            return new Dictionary<RuntimeVariableName, RuntimeValue>
            {
                [new RuntimeVariableName("CommitHash", RuntimeValueType.Scalar)] = this.Hash
            };
        }
    }
}
