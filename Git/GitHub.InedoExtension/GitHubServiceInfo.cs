using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Inedo.Extensibility.Git;
using Inedo.Extensions.GitHub.Clients;

namespace Inedo.Extensions.GitHub
{
    [DisplayName("GitHub")]
    [Description("Provides integration for hosted GitHub repositories.")]
    public sealed class GitHubServiceInfo : GitService<GitHubRepository, GitHubAccount>
    {
        public override string ServiceName => "GitHub";
        public override bool HasDefaultApiUrl => true;
        public override string PasswordDisplayName => "Personal access token";

        public override IAsyncEnumerable<string> GetNamespacesAsync(GitServiceCredentials credentials, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            var client = new GitHubClient(credentials.ServiceUrl, credentials.UserName, credentials.Password, null);
            return client.GetOrganizationsAsync(cancellationToken);
        }
        public override IAsyncEnumerable<string> GetRepositoryNamesAsync(GitServiceCredentials credentials, string serviceNamespace, CancellationToken cancellationToken = default)
        {
            var client = new GitHubClient(credentials.ServiceUrl, credentials.UserName, credentials.Password);
            return client.GetRepositoriesAsync(serviceNamespace, cancellationToken);
        }
    }
}
