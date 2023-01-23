using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Operations.Issues
{
    [DisplayName("Create GitHub Issue")]
    [Description("Creates an issue on a GitHub repository.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Create-Issue")]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    public sealed class GitHubCreateIssueOperation : GitHubOperationBase
    {
        [Required]
        [ScriptAlias("Title")]
        public string Title { get; set; }

        [ScriptAlias("Body")]
        public string Body { get; set; }

        [ScriptAlias("Labels")]
        public IEnumerable<string> Labels { get; set; }

        [ScriptAlias("Assignees")]
        public IEnumerable<string> Assignees { get; set; }

        [ScriptAlias("Milestone")]
        [SuggestableValue(typeof(MilestoneSuggestionProvider))]
        public string Milestone { get; set; }

        [Output]
        [DisplayName("Issue number")]
        [ScriptAlias("Number")]
        public int IssueNumber { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var github = new GitHubClient(credentials, resource, this);
            var data = new Dictionary<string, object> { ["title"] = this.Title };
            if (!string.IsNullOrEmpty(this.Body))
                data.Add("body", this.Body);
            if (this.Labels != null)
                data.Add("labels", this.Labels);
            if (this.Assignees != null)
                data.Add("assignees", this.Assignees);
            if (!string.IsNullOrEmpty(this.Milestone))
                data.Add("milestone", await github.CreateMilestoneAsync(this.Milestone, AH.CoalesceString(resource.OrganizationName, credentials.UserName), resource.RepositoryName, context.CancellationToken));
            this.IssueNumber = await github.CreateIssueAsync(AH.CoalesceString(resource.OrganizationName, credentials.UserName), resource.RepositoryName, data, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Create issue titled ", new Hilite(config[nameof(Title)])),
                new RichDescription("in ", config.DescribeSource())
            );
        }
    }
}
