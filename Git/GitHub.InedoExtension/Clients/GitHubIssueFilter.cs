using System;
using System.Text;

namespace Inedo.Extensions.GitHub.Clients
{
    internal sealed class GitHubIssueFilter
    {
        private string milestone;

        public string Milestone
        {
            get => this.milestone;
            set
            {
                if (value != null && AH.ParseInt(value) == null && value != "*" && !string.Equals("none", value, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("milestone must be an integer, or a string of '*' or 'none'.");

                this.milestone = value;
            }
        }
        public string Labels { get; set; }
        public string CustomFilterQueryString { get; set; }

        public string ToQueryString()
        {
            if (!string.IsNullOrEmpty(this.CustomFilterQueryString))
                return this.CustomFilterQueryString;

            var buffer = new StringBuilder(128);
            buffer.Append("?state=all");
            if (!string.IsNullOrEmpty(this.Milestone))
                buffer.Append("&milestone=" + Uri.EscapeDataString(this.Milestone));
            if (!string.IsNullOrEmpty(this.Labels))
                buffer.Append("&labels=" + Uri.EscapeDataString(this.Labels));
            buffer.Append("&per_page=100");

            return buffer.ToString();
        }
    }
}
