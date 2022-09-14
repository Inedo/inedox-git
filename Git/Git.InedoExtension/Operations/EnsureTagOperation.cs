using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials.Git;

#nullable enable

namespace Inedo.Extensions.Git.Operations
{
    [DisplayName("Tag Code in Git")]
    [Description("Creates and pushes a tag to a git repository.")]
    [Tag("git")]
    [Tag("source-control")]
    [ScriptAlias("Ensure-Tag")]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    public sealed class EnsureTagOperation : CanonicalGitOperation
    {
        [Required]
        [ScriptAlias("Tag")]
        public string? Tag { get; set; }

        [ScriptAlias("Commit")]
        [PlaceholderText("$Commit")]
        public string? Commit { get; set; }

        [Category("Advanced")]
        [ScriptAlias("Force")]
        [DisplayName("Force (overwrite)")]
        public bool Force { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.Commit))
            {
                var commit = await context.ExpandVariablesAsync("$Commit");
                if (!string.IsNullOrWhiteSpace(commit.AsString()))
                    this.Commit = commit.AsString();
                else
                    throw new ExecutionFailureException("Commit was not specified and build source could not be determined from the $Commit variable.");
            }

            if (string.IsNullOrWhiteSpace(this.Commit))
                throw new ExecutionFailureException("Missing required argument: Commit");

            await this.EnsureCommonPropertiesAsync(context);
        }

        protected override async Task<object?> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            using var repo = await this.FetchOrCloneAsync(context);
            this.LogInformation($"Tagging {this.Commit} with {this.Tag}...");
            repo.Tag(this.Commit!, this.Tag!, this.Force);
            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Tag ",
                    new Hilite(AH.CoalesceString(config[nameof(ResourceName)], config[nameof(RepositoryUrl)], "(unknown)"))
                ),
                new RichDescription(
                    "as ",
                    new DirectoryHilite(config[nameof(Tag)])
                )
            );
        }
    }
}
