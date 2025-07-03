using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

#nullable enable

namespace Inedo.Extensions.Git.Operations
{
    [Description("Gets source code from a branch or commit on a git repository.")]
    [ScriptAlias("Checkout-Code")]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    public sealed class CheckoutCodeOperation : CanonicalGitOperation
    {
        [ScriptAlias("BranchOrCommit")]
        [DefaultValue("$Commit")]
        [DisplayName("Commit or branch")]
        public string? Objectish { get; set; }

        [ScriptAlias("To")]
        [DefaultValue("$WorkingDirectory")]
        public string? OutputDirectory { get; set; }

        [DefaultValue(true)]
        [Category("Advanced")]
        [ScriptAlias("RecurseSubmodules")]
        [DisplayName("Recurse submodules")]
        public bool RecurseSubmodules { get; set; } = true;

        [Output]
        [Category("Advanced")]
        [ScriptAlias("CommitHash")]
        [DisplayName("Commit hash")]
        [Description("The full SHA1 hash resolved commit will be stored in this variable. This is useful when you specify a branch for the BranchOrCommit property.")]
        public string? CommitHash { get; set; }

        [Category("Advanced")]
        [ScriptAlias("PreserveLastModified")]
        [DisplayName("Preserve Last Modified Date")]
        [Description("By default, Git will not set the Last Modified date of files when checking out. Selecting this option may take additional time, depending on the number of files in the repository.")]
        public bool PreserveLastModified { get; set; }

        [Category("Advanced")]
        [ScriptAlias("WriteMinimalGitData")]
        [DisplayName("Write minimal git data")]
        [Description("Writes minimal information to the .git directory in the output directory which contains the head commit and the origin url.")]
        public bool WriteMinimalGitData { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.Objectish))
                throw new ExecutionFailureException("Missing required argument: BranchOrCommit");

            await this.EnsureCommonPropertiesAsync(context);

            await base.BeforeRemoteExecuteAsync(context);
        }

        protected override async Task<object?> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            using var repo = await this.FetchOrCloneAsync(context);
            var outputDirectory = context.ResolvePath(this.OutputDirectory);
            this.LogInformation($"Exporting files to {outputDirectory}...");
            return await repo.ExportAsync(new RepoExportOptions(outputDirectory, this.Objectish!, this.RecurseSubmodules, OperatingSystem.IsLinux(), this.PreserveLastModified, this.WriteMinimalGitData), context.CancellationToken);
        }

        protected override Task AfterRemoteExecuteAsync(object? result)
        {
            this.CommitHash = result as string;
            return Task.CompletedTask;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Check out code from ",
                    new Hilite(AH.CoalesceString(config[nameof(ResourceName)], config[nameof(RepositoryUrl)], "Git"))
                ),
                new RichDescription(
                    "to ",
                    new DirectoryHilite(AH.CoalesceString(config[nameof(OutputDirectory)], "working directory"))
                )
            );
        }
    }
}