using System.ComponentModel;
using System.Runtime.CompilerServices;
using Inedo.Extensibility.Git;
using Inedo.Extensions.AzureDevOps.Client;

namespace Inedo.Extensions.AzureDevOps
{
    [DisplayName("Azure DevOps")]
    [Description("Provides integration for hosted Azure DevOps repositories.")]
    public sealed class AzureDevOpsServiceInfo : GitService<AzureDevOpsRepository, AzureDevOpsAccount>
    {
        public override string ServiceName => "Azure DevOps";
        public override bool HasDefaultApiUrl => false;
        public override string PasswordDisplayName => "Personal access token";
        public override string ApiUrlPlaceholderText => "https://dev.azure.com/<my org>";
        public override string ApiUrlDisplayName => "Instance URL";
        public override string NamespaceDisplayName => "Project";

        public override async IAsyncEnumerable<string> GetNamespacesAsync(GitServiceCredentials credentials, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);

            var client = new AzureDevOpsClient(credentials);
            await foreach (var proj in client.GetProjectsAsync(cancellationToken).ConfigureAwait(false))
                yield return proj.Name;
        }
        public override async IAsyncEnumerable<string> GetRepositoryNamesAsync(GitServiceCredentials credentials, string serviceNamespace, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);

            var client = new AzureDevOpsClient(credentials);
            await foreach (var repo in client.GetRepositoriesAsync(serviceNamespace, cancellationToken).ConfigureAwait(false))
                yield return repo.Name;
        }
    }
}
