using System.ComponentModel;
using System.Runtime.CompilerServices;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.AzureDevOps.Client;
using Inedo.Serialization;

namespace Inedo.Extensions.AzureDevOps
{
    [DisplayName("Azure DevOps Project")]
    [Description("Connect to an Azure DevOps project for source code, issue tracking, etc. integration")]
    [PersistFrom("Inedo.Extensions.AzureDevOps.Credentials.AzureDevOpsSecureResource,AzureDevOps")]
    public sealed class AzureDevOpsRepository : GitServiceRepository<AzureDevOpsAccount>, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [DisplayName("[Obsolete] Instance URL")]
        [PlaceholderText("use the credential's URL")]
        [Description("In earlier versions, the Azure DevOps Instance URL was specified on the repository. This should not be used going forward.")]
        public string LegacyInstanceUrl { get; set; }

        [Persistent]
        [DisplayName("Project name")]
        public string ProjectName { get; set; }

        [Persistent]
        [DisplayName("Repository name")]
        [PlaceholderText("use the project name")]
        public override string RepositoryName { get; set; }

        public override string Namespace { get => this.ProjectName; set => this.ProjectName = value; }

        public override RichDescription GetDescription() => new($"{this.ProjectName}/{this.RepositoryName}");

        public override async IAsyncEnumerable<GitPullRequest> GetPullRequestsAsync(ICredentialResolutionContext context, bool includeClosed = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = new AzureDevOpsClient((AzureDevOpsAccount)this.GetCredentials(context));
            await foreach (var r in client.GetPullRequestsAsync(this.ProjectName, this.RepositoryName, cancellationToken))
            {
                if (includeClosed || r.Status != "completed")
                {
                    var sourceRefName = r.SourceRefName;
                    if (sourceRefName == null || !sourceRefName.StartsWith("refs/heads/"))
                        continue;
                    var targetRefName = r.TargetRefName;
                    if (targetRefName == null || !targetRefName.StartsWith("refs/heads/"))
                        continue;

                    sourceRefName = sourceRefName["refs/heads/".Length..];
                    targetRefName = targetRefName["refs/heads/".Length..];

                    yield return new GitPullRequest(r.Id.ToString(), string.Empty, r.Title, r.Status == "completed", sourceRefName, targetRefName);
                }
            }
        }
        public override async IAsyncEnumerable<GitRemoteBranch> GetRemoteBranchesAsync(ICredentialResolutionContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = new AzureDevOpsClient((AzureDevOpsAccount)this.GetCredentials(context));
            await foreach (var b in client.GetBranchesAsync(this.ProjectName, this.RepositoryName, cancellationToken).ConfigureAwait(false))
            {
                if (GitObjectId.TryParse(b.ObjectId, out var hash) && b.Name != null && b.Name.StartsWith("refs/heads/"))
                    yield return new GitRemoteBranch(hash, b.Name["refs/heads/".Length..], b.IsLocked);
            }
        }
        public override async Task<IGitRepositoryInfo> GetRepositoryInfoAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
        {
            var client = new AzureDevOpsClient((AzureDevOpsAccount)this.GetCredentials(context));
            return new RepoInfo(await client.GetRepositoryAsync(this.ProjectName, this.RepositoryName, cancellationToken).ConfigureAwait(false));
        }
        public override Task MergePullRequestAsync(ICredentialResolutionContext context, string id, string headCommit, string commitMessage = null, string method = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
        public override Task SetCommitStatusAsync(ICredentialResolutionContext context, string commit, string status, string description = null, string statusContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (missingProperties.TryGetValue("InstanceUrl", out var url))
                this.LegacyInstanceUrl = url;
        }

        private sealed class RepoInfo : IGitRepositoryInfo
        {
            private readonly AdoRepo repo;

            public RepoInfo(AdoRepo repo) => this.repo = repo;

            public string RepositoryUrl => this.repo.RemoteUrl;
            public string BrowseUrl => this.repo.Links?.Web?.Href;
            public string DefaultBranch => this.repo.DefaultBranch;

            public string GetBrowseUrlForTarget(GitBrowseTarget target)
            {
                return target.Type switch
                {
                    GitBrowseTargetType.Commit => $"{this.BrowseUrl.AsSpan().TrimEnd('/')}/commit/{Uri.EscapeDataString(target.Value)}",
                    GitBrowseTargetType.Branch => $"{this.BrowseUrl}?version=GB{Uri.EscapeDataString(target.Value)}",
                    GitBrowseTargetType.Tag => $"{this.BrowseUrl}?version=GT{Uri.EscapeDataString(target.Value)}",
                    _ => throw new ArgumentOutOfRangeException(nameof(target))
                };
            }
        }
    }
}
