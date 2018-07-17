using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Operations.Issues
{
    [DisplayName("Add Comment to GitHub Issue")]
    [Description("Adds a comment to an issue on GitHub.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Create-IssueComment")]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    public sealed class GitHubCreateIssueCommentOperation : GitHubOperationBase
    {
        [Required]
        [DisplayName("Issue number")]
        [ScriptAlias("Number")]
        public int IssueNumber { get; set; }

        [Required]
        [ScriptAlias("Body")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string Body { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var github = new GitHubClient(this.ApiUrl, this.UserName, this.Password, this.OrganizationName);
            await github.CreateCommentAsync(this.IssueNumber, AH.CoalesceString(this.OrganizationName, this.UserName), this.RepositoryName, this.Body, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Add comment to GitHub issue #", new Hilite(config[nameof(IssueNumber)])),
                new RichDescription(
                    "in ", new Hilite(AH.CoalesceString(config[nameof(RepositoryName)], config[nameof(CredentialName)])),
                    " starting with ", new Hilite(config[nameof(Body)])
                )
            );
        }
    }
}
