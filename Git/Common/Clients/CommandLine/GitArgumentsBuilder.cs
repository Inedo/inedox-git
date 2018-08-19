using System.Collections.Generic;
using System.Linq;

namespace Inedo.Extensions.Clients.CommandLine
{
    internal sealed class GitArgumentsBuilder
    {
        private readonly List<GitArg> arguments = new List<GitArg>(16);

        public GitArgumentsBuilder()
        {
        }

        public GitArgumentsBuilder(string initialArguments)
        {
            this.Append(initialArguments);
        }

        public void Append(string arg) => this.arguments.Add(new GitArg(arg, false, false));
        public void AppendQuoted(string arg) => this.arguments.Add(new GitArg(arg, true, false));
        public void AppendSensitive(string arg) => this.arguments.Add(new GitArg(arg, true, true));

        public override string ToString() => string.Join(" ", this.arguments);
        public string ToSensitiveString() => string.Join(" ", this.arguments.Select(a => a.ToSensitiveString()));

        private sealed class GitArg
        {
            private readonly bool quoted;
            private readonly bool sensitive;
            private readonly string arg;

            public GitArg(string arg, bool quoted, bool sensitive)
            {
                this.arg = arg ?? "";
                this.quoted = quoted;
                this.sensitive = sensitive;
            }

            public override string ToString()
            {
                if (this.quoted)
                    return '"' + Escape(this.arg) + '"';
                else
                    return this.arg;
            }

            public string ToSensitiveString()
            {
                if (this.sensitive)
                    return "(hidden)";
                else if (this.quoted)
                    return '"' + Escape(this.arg) + '"';
                else
                    return this.arg;
            }

            private static string Escape(string s)
            {
                var value = s.Replace("\"", @"\""");
                if (value.EndsWith("\\"))
                    value += "\\";
                return value;
            }
        }
    }
}
