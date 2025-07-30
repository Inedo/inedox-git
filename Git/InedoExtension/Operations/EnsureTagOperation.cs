using System.ComponentModel;
using System.Reflection;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

#nullable enable

namespace Inedo.Extensions.Git.Operations
{
    [Description("Creates and pushes a tag to a git repository.")]
    [ScriptAlias("Ensure-Tag")]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    [DefaultProperty(nameof(Tag))]
    public sealed class EnsureTagOperation : CanonicalGitOperation
    {
        [Required]
        [ScriptAlias("Tag")]
        public string? Tag { get; set; }

        [ScriptAlias("Commit")]
        [DefaultValue("$Commit")]
        public string? Commit { get; set; }

        [Category("Advanced")]
        [ScriptAlias("Force")]
        [DisplayName("Force (overwrite)")]
        public bool Force { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.Commit))
                throw new ExecutionFailureException("Missing required argument: Commit");

            await this.EnsureCommonPropertiesAsync(context);
        }

        protected override async Task<object?> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            using var repo = await this.FetchOrCloneAsync(context);
            this.LogInformation($"Tagging {this.Commit} with {this.Tag}...");
            await repo.TagAsync(this.Commit!, this.Tag!, this.Force, context.CancellationToken);
            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string? val(string name) => AH.NullIf(config[name], this.GetType().GetProperty(name)?.GetCustomAttribute<DefaultValueAttribute>()?.Value?.ToString());

            return new ExtendedRichDescription(
                new RichDescription(
                    "Tag Code in ",
                    new Hilite(AH.CoalesceString(val(nameof(ResourceName)), val(nameof(RepositoryUrl)), "Git"))
                ),
                new RichDescription(
                    "as ",
                    new DirectoryHilite(config[nameof(Tag)])
                )
            );
        }
    }
}
