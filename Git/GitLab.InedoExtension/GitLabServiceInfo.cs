using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Inedo.Extensibility.Git;
using Inedo.Extensions.GitLab.Clients;

namespace Inedo.Extensions.GitLab
{
    [DisplayName("GitLab")]
    [Description("Provides integration for hosted GitLab repositories.")]
    public sealed class GitLabServiceInfo : GitService<GitLabRepository,GitLabAccount>
    {
        public override string ServiceName => "GitLab";
        public override bool HasDefaultApiUrl => true;
        public override string PasswordDisplayName => GitLabClient.PasswordDisplayName;
        public override string ApiUrlDisplayName => GitLabClient.ApiUrlDisplayName;
        public override string ApiUrlPlaceholderText => GitLabClient.ApiUrlPlaceholderText;

        public override IAsyncEnumerable<string> GetNamespacesAsync(GitServiceCredentials credentials, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            var client = new GitLabClient(credentials);
            return client.GetGroupsAsync(cancellationToken);
        }
        public override IAsyncEnumerable<string> GetRepositoryNamesAsync(GitServiceCredentials credentials, string serviceNamespace, CancellationToken cancellationToken = default)
        {
            var client = new GitLabClient(credentials);
            return client.GetProjectsAsync(serviceNamespace, cancellationToken);
        }
    }
}
