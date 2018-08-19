﻿using System;

namespace Inedo.Extensions.Clients
{
    [Serializable]
    public sealed class GitCloneOptions
    {
        public GitCloneOptions()
        {
        }

        public string Branch { get; set; }
        public bool RecurseSubmodules { get; set; }
        public bool IsBare { get; set; }

        public override string ToString()
        {
            return $"Branch={this.Branch ?? "(default)"}; RecurseSubmodules={this.RecurseSubmodules}";
        }
    }
}
