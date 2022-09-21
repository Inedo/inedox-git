using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Inedo.Extensibility.Git;
using Inedo.Extensions.Credentials.Git;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;

namespace Inedo.Extensions.GitHub
{
    [DisplayName("GitHub")]
    [Description("Provides integration for hosted GitHub repositories.")]
    public sealed class GitHubServiceInfo : GitService
    {
        public override Type CredentialType => typeof(GitHubSecureCredentials);
        public override Type ResourceType => typeof(GitHubSecureResource);
        public override string ServiceName => "GitHub";
        public override bool HasDefaultApiUrl => true;
        public override string PasswordDisplayName => "Personal access token";

        public override IAsyncEnumerable<string> GetNamespacesAsync(GitSecureCredentialsBase credentials, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            var client = new GitHubClient(credentials.Url, credentials.UserName, credentials.Password, null);
            return client.GetOrganizationsAsync(cancellationToken);
        }
        public override IAsyncEnumerable<string> GetRepositoryNamesAsync(GitSecureCredentialsBase credentials, string serviceNamespace, CancellationToken cancellationToken = default)
        {
            var client = new GitHubClient(credentials.Url, credentials.UserName, credentials.Password, serviceNamespace);
            return client.GetRepositoriesAsync(cancellationToken);
        }
    }
}
