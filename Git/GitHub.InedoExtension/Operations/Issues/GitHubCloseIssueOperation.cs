using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.Clients;

namespace Inedo.Extensions.GitHub.Operations.Issues
{
    [DisplayName("Close GitHub Issue")]
    [Description("Closes an issue on GitHub.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Close-Issue")]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    public sealed class GitHubCloseIssueOperation : GitHubOperationBase
    {
        [Required]
        [DisplayName("Issue number")]
        [ScriptAlias("Number")]
        public int IssueNumber { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var github = new GitHubClient(credentials, resource, this);
            await github.UpdateIssueAsync(this.IssueNumber, AH.CoalesceString(resource.OrganizationName, credentials.UserName), resource.RepositoryName, new { state = "closed" }, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Close GitHub issue #", new Hilite(config[nameof(IssueNumber)])),
                new RichDescription("in ", new Hilite(config.DescribeSource()))
            );
        }
    }
}
