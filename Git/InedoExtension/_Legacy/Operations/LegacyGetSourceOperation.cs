﻿using System.ComponentModel;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Clients;
using Inedo.Web;

namespace Inedo.Extensions.Operations
{
    [Obsolete]
    public abstract class LegacyGetSourceOperation : LegacyGitOperation
    {
        [ScriptAlias("DiskPath")]
        [DisplayName("Export to directory")]
        [PlaceholderText("$WorkingDirectory")]
        [FieldEditMode(FieldEditMode.ServerDirectoryPath)]
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
        [ScriptAlias("PreserveLastModified")]
        [DisplayName("Preserve Last Modified Date")]
        [Description("By default, Git will not set the Last Modified date of files when checking out. Selecting this option may take additional time, depending on the number of files in the repository.")]
        public bool PreserveLastModified { get; set; }

        [Category("Advanced")]
        [ScriptAlias("KeepInternals")]
        [DisplayName("Copy internal Git files")]
        [Description("When exporting the repository, also export .git* files.")]
        public bool KeepInternals { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogWarning("This operation has been deprecated and may be removed in a future version. Use Git::Checkout-Code instead.");

            var (creds, resource) = this.GetCredentialsAndResource(context);

            string repositoryUrl = (await resource.GetRepositoryInfoAsync(context, context.CancellationToken).ConfigureAwait(false)).RepositoryUrl;
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

            var client = this.CreateClient(context, repositoryUrl, workspacePath, creds);
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

            var diskPath = context.ResolvePath(this.DiskPath);
            await client.ArchiveAsync(diskPath, this.KeepInternals).ConfigureAwait(false);

            if (this.PreserveLastModified)
            {
                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
                var files = await client.ListRepoFilesAsync().ConfigureAwait(false);

                foreach (var file in files)
                {
                    var modTime = await client.GetFileLastModifiedAsync(file).ConfigureAwait(false);
                    var filePath = fileOps.CombinePath(diskPath, file);
                    if (modTime.HasValue && fileOps.FileExists(filePath))
                    {
                        await fileOps.SetLastWriteTimeAsync(filePath, modTime.Value.UtcDateTime).ConfigureAwait(false);
                    }
                }
            }

            this.LogInformation("Get source complete.");
        }
    }
}
