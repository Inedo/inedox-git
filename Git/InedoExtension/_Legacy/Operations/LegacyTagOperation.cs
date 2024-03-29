﻿using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Clients;

namespace Inedo.Extensions.Operations
{
    [Obsolete]
    public abstract class LegacyTagOperation : LegacyGitOperation
    {
        [Required]
        [ScriptAlias("Tag")]
        [DisplayName("Tag name")]
        public string Tag { get; set; }

        [ScriptAlias("Message")]
        [DisplayName("Tag message")]
        [PlaceholderText("none (lightweight tag)")]
        [Description("When this is not specified, a lightweight tag is created.")]
        public string TagMessage { get; set; }

        [ScriptAlias("Branch")]
        [DisplayName("Branch name")]
        [PlaceholderText("default")]
        public string Branch { get; set; }

        [Category("Advanced")]
        [ScriptAlias("CommitHash")]
        [DisplayName("Commit hash")]
        [Description("The tag will refer to this commit if specified; otherwise it will refer to the current head.")]
        public string CommitHash { get; set; }

        [Category("Advanced")]
        [ScriptAlias("Force")]
        [Description("Overwrite another tag that has the same name; otherwise using an existing tag name is an error.")]
        public bool Force { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogWarning("This operation has been deprecated and may be removed in a future version. Use Git::Ensure-Tag instead.");

            var (creds, resource) = this.GetCredentialsAndResource(context);

            string repositoryUrl = (await resource.GetRepositoryInfoAsync(context, context.CancellationToken)).RepositoryUrl;
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                this.LogError("RepositoryUrl is not specified. It must be included in either the referenced credential or in the RepositoryUrl argument of the operation.");
                return;
            }

            string branchDesc = string.IsNullOrEmpty(this.Branch) ? "" : $" on '{this.Branch}' branch";
            this.LogInformation($"Tag '{repositoryUrl}'{branchDesc} as '{this.Tag}'...");

            var client = this.CreateClient(context, repositoryUrl, WorkspacePath.Resolve(context, repositoryUrl, this.WorkspaceDiskPath), creds);
            bool valid = await client.IsRepositoryValidAsync().ConfigureAwait(false);
            if (!valid)
            {
                await client.CloneAsync(
                    new GitCloneOptions
                    {
                        Branch = this.Branch,
                        RecurseSubmodules = this.RecurseSubmodules
                    }
                ).ConfigureAwait(false);
            }

            await client.UpdateAsync(
                new GitUpdateOptions
                {
                    RecurseSubmodules = this.RecurseSubmodules,
                    Branch = this.Branch,
                    Ref = this.CommitHash
                }
            ).ConfigureAwait(false);

            await client.TagAsync(this.Tag, this.CommitHash, this.TagMessage, this.Force).ConfigureAwait(false);

            this.LogInformation("Tag complete.");
        }
    }
}
