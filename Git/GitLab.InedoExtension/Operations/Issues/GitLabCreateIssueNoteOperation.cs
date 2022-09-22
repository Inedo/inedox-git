using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Operations.Issues
{
    [DisplayName("Add Note to GitLab Issue")]
    [Description("Adds a note to an issue on GitLab.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Create-IssueNote")]
    [ScriptNamespace("GitLab", PreferUnqualified = false)]
    public sealed class GitLabCreateIssueNoteOperation : GitLabOperationBase
    {
        [Required]
        [DisplayName("Issue ID")]
        [ScriptAlias("IssueId")]
        public int IssueId { get; set; }

        [Required]
        [ScriptAlias("Body")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string Body { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var gitlab = new GitLabClient(credentials, resource);
            await gitlab.CreateCommentAsync(this.IssueId, resource, this.Body, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Add comment to GitLab issue #", new Hilite(config[nameof(IssueId)])),
                new RichDescription(
                    "in ", new Hilite(config.DescribeSource()),
                    " starting with ", new Hilite(config[nameof(Body)])
                )
            );
        }
    }
}
