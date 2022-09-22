using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Operations.Issues
{
    [DisplayName("Create GitLab Issue")]
    [Description("Creates an issue on a GitLab project.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Create-Issue")]
    [ScriptNamespace("GitLab", PreferUnqualified = false)]
    public sealed class GitLabCreateIssueOperation : GitLabOperationBase
    {
        [Required]
        [ScriptAlias("Title")]
        public string Title { get; set; }

        [DisplayName("Description")]
        [ScriptAlias("Description")]
        public string Body { get; set; }

        [ScriptAlias("Labels")]
        public IEnumerable<string> Labels { get; set; }

        [ScriptAlias("Assignees")]
        public IEnumerable<string> Assignees { get; set; }

        [ScriptAlias("Milestone")]
        [SuggestableValue(typeof(MilestoneSuggestionProvider))]
        public string Milestone { get; set; }

        [DisplayName("Additional properties")]
        [ScriptAlias("AdditionalProperties")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Example("%(weight: 1000, confidential: true)")]
        public IReadOnlyDictionary<string, object> AdditionalProperties { get; set; }

        [Output]
        [DisplayName("Issue ID")]
        [ScriptAlias("IssueId")]
        public string IssueId { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var gitlab = new GitLabClient(credentials, resource);
            var data = new Dictionary<string, object> { ["title"] = this.Title };
            if (this.AdditionalProperties != null)
                foreach (var p in this.AdditionalProperties)
                    data.Add(p.Key, p.Key == "confidential" ? bool.Parse(p.Value?.ToString()) : p.Key == "weight" ? int.Parse(p.Value?.ToString()) : p.Value);
            if (!string.IsNullOrEmpty(this.Body))
                data.Add("description", this.Body);
            if (this.Labels != null)
                data.Add("labels", string.Join(",", this.Labels));
            if (this.Assignees != null)
                data.Add("assignee_ids", (await Task.WhenAll(this.Assignees.Select(name => gitlab.FindUserAsync(name, context.CancellationToken))).ConfigureAwait(false)).Where(id => id.HasValue));
            if (!string.IsNullOrEmpty(this.Milestone))
                data.Add("milestone_id", await gitlab.CreateMilestoneAsync(this.Milestone, resource, context.CancellationToken).ConfigureAwait(false));
            this.IssueId = (await gitlab.CreateIssueAsync(resource, data, context.CancellationToken).ConfigureAwait(false)).ToString();
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Create issue titled ", new Hilite(config[nameof(Title)])),
                new RichDescription("in ", new Hilite(config.DescribeSource()))
            );
        }
    }
}
