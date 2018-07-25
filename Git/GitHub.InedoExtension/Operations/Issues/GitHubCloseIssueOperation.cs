﻿using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
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
            var github = new GitHubClient(this.ApiUrl, this.UserName, this.Password, this.OrganizationName);
            await github.UpdateIssueAsync(this.IssueNumber, AH.CoalesceString(this.OrganizationName, this.UserName), this.RepositoryName, new { state = "closed" }, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Close GitHub issue #", new Hilite(config[nameof(IssueNumber)])),
                new RichDescription("in ", new Hilite(AH.CoalesceString(config[nameof(RepositoryName)], config[nameof(CredentialName)])))
            );
        }
    }
}
