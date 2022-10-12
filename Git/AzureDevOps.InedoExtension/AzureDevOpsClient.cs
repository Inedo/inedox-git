using System.Runtime.CompilerServices;
using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

#nullable enable

namespace Inedo.Extensions.AzureDevOps
{
    internal sealed class AzureDevOpsClient : IDisposable
    {
        private readonly VssConnection connection;
        private GitHttpClient? gitClient;
        private ProjectHttpClient? projClient;
        private BuildHttpClient? buildClient;
        private WorkHttpClient? workClient;
        private bool disposed;

        public AzureDevOpsClient(IAzureDevOpsConfiguration config, ICredentialResolutionContext context)
        {
            var (c, r) = config.GetCredentialsAndResource(context);
            var cred = new VssBasicCredential(string.Empty, AH.CoalesceString(AH.Unprotect(config.Token), AH.Unprotect(c.Password)));
            this.connection = new VssConnection(new Uri(AH.CoalesceString(config.InstanceUrl, c.ServiceUrl, r.LegacyInstanceUrl)!), cred);
        }
        public AzureDevOpsClient(GitServiceCredentials credentials) : this(credentials.ServiceUrl!, credentials.Password!)
        {
        }
        public AzureDevOpsClient(string url, SecureString token) : this(url, AH.Unprotect(token)!)
        {
        }
        public AzureDevOpsClient(string url, string token)
        {
            var cred = new VssBasicCredential(string.Empty, token);
            this.connection = new VssConnection(new Uri(url), cred);
        }

        public async IAsyncEnumerable<string> GetProjectsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = await this.GetProjectHttpClient(cancellationToken).ConfigureAwait(false);
            foreach (var proj in await client.GetProjects().ConfigureAwait(false))
                yield return proj.Name;
        }
        public async IAsyncEnumerable<string> GetRepositoryNamesAsync(string projectName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = await this.GetGitHttpClient(cancellationToken).ConfigureAwait(false);
            foreach (var repo in await client.GetRepositoriesAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(repo.ProjectReference.Name, projectName, StringComparison.OrdinalIgnoreCase))
                    yield return repo.Name;
            }
        }

        public async Task<IGitRepositoryInfo> GetRepositoryInfoAsync(string projectName, string repositoryName, CancellationToken cancellationToken = default)
        {
            var client = await this.GetGitHttpClient(cancellationToken).ConfigureAwait(false);
            foreach (var repo in await client.GetRepositoriesAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(repo.ProjectReference.Name, projectName, StringComparison.OrdinalIgnoreCase) && string.Equals(repo.Name, repositoryName, StringComparison.OrdinalIgnoreCase))
                    return new RepoInfo(repo);
            }

            throw new ArgumentException($"Could not find repository {repositoryName} in project {projectName}.");
        }

        public async IAsyncEnumerable<string> GetBuildDefinitionsAsync(string projectName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = await this.GetBuildHttpClient(cancellationToken).ConfigureAwait(false);
            var definitions = await client.GetDefinitionsAsync(projectName, cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (var d in definitions)
                yield return d.Name;
        }

        public async IAsyncEnumerable<GitRemoteBranch> GetBranchesAsync(string projectName, string repositoryName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = await this.GetGitHttpClient(cancellationToken).ConfigureAwait(false);
            foreach (var b in await client.GetBranchesAsync(projectName, repositoryName, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (!GitObjectId.TryParse(b.Commit.CommitId, out var hash))
                    continue;

                yield return new GitRemoteBranch(hash, b.Name);
            }
        }

        public async IAsyncEnumerable<string> GetBuildsAsync(string projectName, string buildDefinition, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = await this.GetBuildHttpClient(cancellationToken).ConfigureAwait(false);

            int? definitionId = null;

            foreach (var d in await client.GetDefinitionsAsync(projectName, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(d.Name, projectName, StringComparison.OrdinalIgnoreCase))
                {
                    definitionId = d.Id;
                    break;
                }
            }

            if (definitionId == null)
                yield break;

            foreach (var b in await client.GetBuildsAsync(new[] { definitionId.Value }, cancellationToken: cancellationToken).ConfigureAwait(false))
                yield return b.BuildNumber;
        }

        public async Task<Stream?> DownloadArtifactAsync(string projectName, string buildDefinition, string buildNumber, string artifactName, CancellationToken cancellationToken = default)
        {
            var client = await this.GetBuildHttpClient(cancellationToken).ConfigureAwait(false);

            int? definitionId = null;

            foreach (var d in await client.GetDefinitionsAsync(projectName, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(d.Name, projectName, StringComparison.OrdinalIgnoreCase))
                {
                    definitionId = d.Id;
                    break;
                }
            }

            if (definitionId == null)
                return null;

            int? buildId = null;

            foreach (var b in await client.GetBuildsAsync(new[] { definitionId.Value }, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(b.BuildNumber, buildNumber, StringComparison.OrdinalIgnoreCase))
                {
                    buildId = b.Id;
                    break;
                }
            }

            if (buildId == null)
                return null;

            return await client.GetArtifactContentZipAsync(projectName, buildId.Value, artifactName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<Extensibility.Git.GitPullRequest> GetPullRequestsAsync(string projectName, string repositoryName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = await this.GetGitHttpClient(cancellationToken).ConfigureAwait(false);
            foreach (var r in await client.GetPullRequestsAsync(projectName, repositoryName, new GitPullRequestSearchCriteria(), cancellationToken: cancellationToken).ConfigureAwait(false))
                yield return new Extensibility.Git.GitPullRequest(r.PullRequestId.ToString(), r.Url, r.Title, r.ClosedBy != null, r.SourceRefName, r.TargetRefName);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.gitClient?.Dispose();
                this.projClient?.Dispose();
                this.buildClient?.Dispose();
                this.workClient?.Dispose();
                this.connection.Dispose();
                this.disposed = true;
            }
        }

        private async ValueTask<GitHttpClient> GetGitHttpClient(CancellationToken cancellationToken)
        {
            this.gitClient ??= await this.connection.GetClientAsync<GitHttpClient>(cancellationToken).ConfigureAwait(false);
            return this.gitClient;
        }
        private async ValueTask<ProjectHttpClient> GetProjectHttpClient(CancellationToken cancellationToken)
        {
            this.projClient ??= await this.connection.GetClientAsync<ProjectHttpClient>(cancellationToken).ConfigureAwait(false);
            return this.projClient;
        }
        private async ValueTask<BuildHttpClient> GetBuildHttpClient(CancellationToken cancellationToken)
        {
            this.buildClient ??= await this.connection.GetClientAsync<BuildHttpClient>(cancellationToken).ConfigureAwait(false);
            return this.buildClient;
        }
        private async ValueTask<WorkHttpClient> GetWorkHttpClient(CancellationToken cancellationToken)
        {
            this.workClient ??= await this.connection.GetClientAsync<WorkHttpClient>(cancellationToken).ConfigureAwait(false);
            return this.workClient;
        }

        private sealed class RepoInfo : IGitRepositoryInfo
        {
            public RepoInfo(Microsoft.TeamFoundation.SourceControl.WebApi.GitRepository repo)
            {
                this.RepositoryUrl = repo.RemoteUrl;
                this.BrowseUrl = repo.WebUrl;
                this.DefaultBranch = repo.DefaultBranch;
            }

            public string RepositoryUrl { get; }
            public string BrowseUrl { get; }
            public string DefaultBranch { get; }

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
