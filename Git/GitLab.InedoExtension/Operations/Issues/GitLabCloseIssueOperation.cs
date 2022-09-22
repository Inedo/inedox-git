using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.Clients;

namespace Inedo.Extensions.GitLab.Operations.Issues
{
    [DisplayName("Close GitLab Issue")]
    [Description("Closes an issue on GitLab.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Close-Issue")]
    [ScriptNamespace("GitLab", PreferUnqualified = false)]
    public sealed class GitLabCloseIssueOperation : GitLabOperationBase
    {
        [Required]
        [DisplayName("Issue ID")]
        [ScriptAlias("IssueId")]
        public int IssueId { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var gitlab = new GitLabClient(credentials, resource);
            await gitlab.UpdateIssueAsync(this.IssueId, resource, new { state_event = "close" }, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Close GitLab issue #", new Hilite(config[nameof(IssueId)])),
                new RichDescription("in ", new Hilite(config.DescribeSource()))
            );
        }
    }
}
