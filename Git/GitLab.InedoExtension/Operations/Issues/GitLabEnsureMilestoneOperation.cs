using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Configurations;

namespace Inedo.Extensions.GitLab.Operations.Issues
{
    [DisplayName("Ensure GitLab Milestone")]
    [Description("Ensures a GitLab milestone exists with the specified properties.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Ensure-Milestone")]
    [ScriptNamespace("GitLab", PreferUnqualified = false)]
    public sealed class GitLabEnsureMilestoneOperation : EnsureOperation<GitLabMilestoneConfiguration>
    {
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var (credentials, resource) = this.Template.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var gitlab = new GitLabClient(credentials, resource);

            GitLabMilestone milestone = null;

            await foreach (var m in gitlab.GetMilestonesAsync(resource, null, context.CancellationToken))
            {
                if (string.Equals(m.Title, this.Template.Title, StringComparison.OrdinalIgnoreCase))
                {
                    milestone = m;
                    break;
                }
            }

            if (milestone == null)
            {
                return new GitLabMilestoneConfiguration
                {
                    Exists = false
                };
            }

            return new GitLabMilestoneConfiguration
            {
                Exists = true,
                Title = milestone.Title,
                Description = milestone.Description ?? string.Empty,
                StartDate = milestone.StartDate,
                DueDate = milestone.DueDate,
                State = (GitLabMilestoneConfiguration.OpenOrClosed)Enum.Parse(typeof(GitLabMilestoneConfiguration.OpenOrClosed), milestone.State)
            };
        }

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.Template.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var gitlab = new GitLabClient(credentials, resource);
            var id = await gitlab.CreateMilestoneAsync(this.Template.Title, resource, context.CancellationToken).ConfigureAwait(false);

            var data = new Dictionary<string, object> { ["title"] = this.Template.Title };
            if (this.Template.StartDate != null)
                data.Add("start_date", AH.NullIf(this.Template.StartDate, string.Empty));
            if (this.Template.DueDate != null)
                data.Add("due_date", AH.NullIf(this.Template.DueDate, string.Empty));
            if (this.Template.Description != null)
                data.Add("description", this.Template.Description);
            if (this.Template.State.HasValue)
                data.Add("state_event", this.Template.State == GitLabMilestoneConfiguration.OpenOrClosed.open ? "activate" : "close");

            await gitlab.UpdateMilestoneAsync(id, resource, data, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Ensure milestone ", new Hilite(config[nameof(GitLabMilestoneConfiguration.Title)])),
                new RichDescription("in ", new Hilite(config.DescribeSource()))
            );
        }
    }
}
