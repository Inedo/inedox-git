using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

#nullable enable

namespace Inedo.Extensions.Git.Operations
{
    [DisplayName("Check Out Code from Git")]
    [Description("Gets source code from a git repository.")]
    [Tag("git")]
    [Tag("source-control")]
    [ScriptAlias("Checkout-Code")]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    public sealed class CheckoutCodeOperation : CanonicalGitOperation
    {
        [ScriptAlias("To")]
        [PlaceholderText("$WorkingDirectory")]
        public string? OutputDirectory { get; set; }

        [ScriptAlias("BranchOrCommit")]
        [PlaceholderText("default")]
        public string? Objectish { get; set; }

        [DefaultValue(true)]
        [Category("Advanced")]
        [ScriptAlias("RecurseSubmodules")]
        [DisplayName("Recurse submodules")]
        public bool RecurseSubmodules { get; set; } = true;

        [Output]
        [Category("Advanced")]
        [ScriptAlias("CommitHash")]
        [DisplayName("Commit hash")]
        [Description("The full SHA1 hash resolved commit will be stored in this variable.")]
        public string? CommitHash { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.Objectish))
            {
                var commit = await context.ExpandVariablesAsync("$Commit");
                if (!string.IsNullOrWhiteSpace(commit.AsString()))
                {
                    this.Objectish = commit.AsString();
                }
                else
                {
                    var branch = await context.ExpandVariablesAsync("$Branch");
                    if (!string.IsNullOrWhiteSpace(branch.AsString()))
                        this.Objectish = branch.AsString();
                    else
                        throw new ExecutionFailureException("BranchOrCommit was not specified and build source could not be determined from the $Commit or $Branch variables.");
                }
            }

            if (string.IsNullOrWhiteSpace(this.Objectish))
                throw new ExecutionFailureException("Missing required argument: BranchOrCommit");

            await this.EnsureCommonPropertiesAsync(context);
        }

        protected override async Task<object?> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            using var repo = await this.FetchOrCloneAsync(context);
            var outputDirectory = context.ResolvePath(this.OutputDirectory);
            this.LogInformation($"Exporting files to {outputDirectory}...");
            await repo.ExportAsync(outputDirectory, this.Objectish!, this.RecurseSubmodules, OperatingSystem.IsLinux(), context.CancellationToken);
            return repo.GetCommitHash(this.Objectish!);
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
                    new Hilite(AH.CoalesceString(config[nameof(ResourceName)], config[nameof(RepositoryUrl)], "(unknown)"))
                ),
                new RichDescription(
                    "to ",
                    new DirectoryHilite(config[nameof(OutputDirectory)])
                )
            );
        }
    }
}