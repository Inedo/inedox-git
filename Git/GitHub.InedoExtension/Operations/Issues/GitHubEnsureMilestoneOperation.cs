﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Configurations;

namespace Inedo.Extensions.GitHub.Operations.Issues
{
    [DisplayName("Ensure GitHub Milestone")]
    [Description("Ensures a GitHub milestone exists with the specified properties.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Ensure-Milestone")]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    public sealed class GitHubEnsureMilestoneOperation : EnsureOperation<GitHubMilestoneConfiguration>
    {
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var github = new GitHubClient(this.Template.ApiUrl, this.Template.UserName, this.Template.Password, this.Template.OrganizationName);
            var milestones = await github.GetMilestonesAsync(AH.CoalesceString(this.Template.OrganizationName, this.Template.UserName), this.Template.RepositoryName, null, context.CancellationToken).ConfigureAwait(false);
            var milestone = milestones.FirstOrDefault(m => string.Equals(m["title"]?.ToString() ?? string.Empty, this.Template.Title, StringComparison.OrdinalIgnoreCase));
            if (milestone == null)
            {
                return new GitHubMilestoneConfiguration
                {
                    Exists = false
                };
            }

            return new GitHubMilestoneConfiguration
            {
                Exists = true,
                Title = milestone["title"]?.ToString() ?? string.Empty,
                Description = milestone["description"]?.ToString() ?? string.Empty,
                DueDate = milestone["due_on"]?.ToString()?.Substring(0, 10),
                State = (GitHubMilestoneConfiguration.OpenOrClosed)Enum.Parse(typeof(GitHubMilestoneConfiguration.OpenOrClosed), milestone["state"]?.ToString())
            };
        }

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var github = new GitHubClient(this.Template.ApiUrl, this.Template.UserName, this.Template.Password, this.Template.OrganizationName);
            var number = await github.CreateMilestoneAsync(this.Template.Title, AH.CoalesceString(this.Template.OrganizationName, this.Template.UserName), this.Template.RepositoryName, context.CancellationToken).ConfigureAwait(false);

            var data = new Dictionary<string, object> { ["title"] = this.Template.Title };
            if (this.Template.DueDate != null)
                data.Add("due_on", AH.NullIf(AH.ConcatNE(this.Template.DueDate, "T", DateTime.UtcNow.ToString("HH:mm:ss"), "Z"), string.Empty));
            if (this.Template.Description != null)
                data.Add("description", this.Template.Description);
            if (this.Template.State.HasValue)
                data.Add("state", this.Template.State.ToString());

            await github.UpdateMilestoneAsync(number, AH.CoalesceString(this.Template.OrganizationName, this.Template.UserName), this.Template.RepositoryName, data, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Ensure milestone ", new Hilite(config[nameof(GitHubMilestoneConfiguration.Title)])),
                new RichDescription("in ", new Hilite(AH.CoalesceString(config[nameof(GitHubMilestoneConfiguration.RepositoryName)], config[nameof(GitHubMilestoneConfiguration.CredentialName)])))
            );
        }
    }
}
