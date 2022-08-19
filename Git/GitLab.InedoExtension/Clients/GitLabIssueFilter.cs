using System;
using System.Text;

namespace Inedo.Extensions.GitLab.Clients
{
    internal sealed class GitLabIssueFilter
    {
        public string Milestone { get; set; }
        public string Labels { get; set; }
        public string CustomFilterQueryString { get; set; }

        public string ToQueryString()
        {
            if (!string.IsNullOrEmpty(this.CustomFilterQueryString))
                return this.CustomFilterQueryString;

            var buffer = new StringBuilder("?per_page=100", 128);
            if (!string.IsNullOrEmpty(this.Milestone))
                buffer.Append("&milestone=").Append(Uri.EscapeDataString(this.Milestone));
            if (!string.IsNullOrEmpty(this.Labels))
                buffer.Append("&labels=").Append(Uri.EscapeDataString(this.Labels));

            return buffer.ToString();
        }
    }
}
