using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Credentials.Git;
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
        public override Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken) => Task.FromResult(this.RepositoryUrl);
   }
}
