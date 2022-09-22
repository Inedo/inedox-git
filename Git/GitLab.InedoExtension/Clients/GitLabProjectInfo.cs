using System;
using Inedo.Extensibility.Git;

namespace Inedo.Extensions.GitLab.Clients
{
    internal sealed class GitLabProjectInfo : IGitRepositoryInfo
    {
        public string RepositoryUrl { get; init; }
        public string BrowseUrl { get; init; }
        public string DefaultBranch { get; init; }

        public string GetBrowseUrlForTarget(GitBrowseTarget target)
        {
            var url = this.BrowseUrl.AsSpan().TrimEnd('/');

            return target.Type switch
            {
                GitBrowseTargetType.Commit => $"{url}/-/commit/{target.Value}",
                GitBrowseTargetType.Tag => $"{url}/-/tags/{Uri.EscapeDataString(target.Value)}",
                GitBrowseTargetType.Branch => $"{url}/-/tree/{Uri.EscapeDataString(target.Value)}",
                _ => null
            };
        }
    }
}
