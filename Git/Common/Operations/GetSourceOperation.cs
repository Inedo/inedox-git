using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Web.Plans.ArgumentEditors;

namespace Inedo.Extensions.Operations
{
    public abstract class GetSourceOperation<TCredentials> : GetSourceOperation, IHasCredentials<TCredentials> where TCredentials : GitCredentialsBase, new()
    {
        public abstract string CredentialName { get; set; }

    }
    public abstract class GetSourceOperation : GitOperation
    {
        [ScriptAlias("DiskPath")]
        [DisplayName("Export to directory")]
        [FilePathEditor]
        [PlaceholderText("$WorkingDirectory")]
        public string DiskPath { get; set; }

        [ScriptAlias("Branch")]
        [DisplayName("Branch name")]
        [PlaceholderText("default")]
        public string Branch { get; set; }
        [ScriptAlias("Ref")]
        [ScriptAlias("Tag", Obsolete = true)]
        [DisplayName("Reference")]
        [Description("A reference such as a tag name or a commit hash.")]
        public string Ref { get; set; }

        [Output]
        [Category("Advanced")]
        [ScriptAlias("CommitHash")]
        [DisplayName("Commit hash")]
        [Description("The full SHA1 hash of the fetched commit will be stored in this variable.")]
        public string CommitHash { get; set; }

        [Category("Advanced")]
        [ScriptAlias("KeepInternals")]
        [DisplayName("Copy internal Git files")]
        [Description("When exporting the repository, also export .git* files.")]
        public bool KeepInternals { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            string repositoryUrl = await this.GetRepositoryUrlAsync(context.CancellationToken, context as ICredentialResolutionContext).ConfigureAwait(false);
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                this.LogError("RepositoryUrl is not specified. It must be included in either the referenced credential or in the RepositoryUrl argument of the operation.");
                return;
            }

            string branchDesc = string.IsNullOrEmpty(this.Branch) ? "" : $" on '{this.Branch}' branch";
            string refDesc = string.IsNullOrEmpty(this.Ref) ? "" : $", commit '{this.Ref}'";
            this.LogInformation($"Getting source from '{repositoryUrl}'{branchDesc}{refDesc}...");

            var workspacePath = WorkspacePath.Resolve(context, repositoryUrl, this.WorkspaceDiskPath);

            if (this.CleanWorkspace)
            {
                this.LogDebug($"Clearing workspace path '{workspacePath.FullPath}'...");
                var fileOps = context.Agent.GetService<IFileOperationsExecuter>();
                await fileOps.ClearDirectoryAsync(workspacePath.FullPath).ConfigureAwait(false);
            }

            var client = this.CreateClient(context, repositoryUrl, workspacePath);
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

            this.CommitHash = await client.UpdateAsync(
                new GitUpdateOptions
                {
                    RecurseSubmodules = this.RecurseSubmodules,
                    Branch = this.Branch,
                    Ref = this.Ref
                }
            ).ConfigureAwait(false);

            this.LogDebug($"Current commit is {this.CommitHash}.");

            await client.ArchiveAsync(context.ResolvePath(this.DiskPath), this.KeepInternals).ConfigureAwait(false);

            this.LogInformation("Get source complete.");
        }
    }
}
