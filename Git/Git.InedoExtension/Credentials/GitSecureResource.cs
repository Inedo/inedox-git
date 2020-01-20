using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Git.Credentials
{
    [DisplayName("Git Repository")]
    [Description("Connect to a Git repository for source code integration")]
    public sealed class GitSecureResource : GitSecureResourceBase<Extensions.Credentials.UsernamePasswordCredentials, GitSecureCredentialsBase>
    {
        [Persistent]
        [DisplayName("Repository URL")]
        public string RepositoryUrl { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.RepositoryUrl);
        public override Task<string> GetRepositoryUrl(ICredentialResolutionContext context, CancellationToken cancellationToken) => Task.FromResult(this.RepositoryUrl);
    }
}
