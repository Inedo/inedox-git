using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Inedo.Extensibility.Git;
using Inedo.Extensions.Credentials.Git;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;

namespace Inedo.Extensions.GitLab
{
    [DisplayName("GitLab")]
    [Description("Provides integration for hosted GitLab repositories.")]
    public sealed class GitLabServiceInfo : GitService
    {
        public override Type CredentialType => typeof(GitLabSecureCredentials);
        public override Type ResourceType => typeof(GitLabSecureResource);
        public override string ServiceName => "GitLab";
        public override bool HasDefaultApiUrl => true;
        public override string PasswordDisplayName => GitLabClient.PasswordDisplayName;
        public override string ApiUrlDisplayName => GitLabClient.ApiUrlDisplayName;
        public override string ApiUrlPlaceholderText => GitLabClient.ApiUrlPlaceholderText;

        public override IAsyncEnumerable<string> GetNamespacesAsync(GitSecureCredentialsBase credentials, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            var client = new GitLabClient(credentials.Url, credentials.UserName, credentials.Password, null);
            return client.GetGroupsAsync(cancellationToken);
        }
        public override IAsyncEnumerable<string> GetRepositoryNamesAsync(GitSecureCredentialsBase credentials, string serviceNamespace, CancellationToken cancellationToken = default)
        {
            var client = new GitLabClient(credentials.Url, credentials.UserName, credentials.Password, serviceNamespace);
            return client.GetProjectsAsync(cancellationToken);
        }
    }
}
