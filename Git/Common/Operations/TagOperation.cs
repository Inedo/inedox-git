using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Operations;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations
{
    public abstract class TagOperation : GitOperation, IHasCredentials<GitCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [ScriptAlias("RepositoryUrl")]
        [DisplayName("Repository URL")]
        [PlaceholderText("Use repository from credentials")]
        [MappedCredential(nameof(GitCredentials.RepositoryUrl))]
        public string RepositoryUrl { get; set; }

        [Required]
        [ScriptAlias("Tag")]
        [DisplayName("Tag name")]
        public string Tag { get; set; }

        [ScriptAlias("Branch")]
        [DisplayName("Branch name")]
        [PlaceholderText("default")]
        public string Branch { get; set; }        

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            string repositoryUrl = this.GetRepositoryUrl();
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                this.LogError("RepositoryUrl is not specified. It must be included in either the referenced credential or in the RepositoryUrl argument of the operation.");
                return;
            }

            string branchDesc = string.IsNullOrEmpty(this.Branch) ? "" : $" on '{this.Branch}' branch";
            this.LogInformation($"Tag '{repositoryUrl}'{branchDesc} as '{this.Tag}'...");

            var client = this.CreateClient(context, repositoryUrl, WorkspacePath.Resolve(context, repositoryUrl, this.WorkspaceDiskPath));
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
                    Branch = this.Branch
                }
            ).ConfigureAwait(false);

            await client.TagAsync(this.Tag).ConfigureAwait(false);

            this.LogInformation("Tag complete.");
        }

        protected virtual string GetRepositoryUrl()
        {
            return this.RepositoryUrl;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Tag Git Source"),
               new RichDescription("in ", new Hilite(config[nameof(this.RepositoryUrl)]), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
