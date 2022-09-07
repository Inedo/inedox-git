using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials.Git;
using Inedo.Web;

#nullable enable

namespace Inedo.Extensions.Git.Operations
{
    [DisplayName("Tag Code in Git")]
    [Description("Creates and pushes a tag to a git repository.")]
    [Tag("git")]
    [Tag("source-control")]
    [ScriptAlias("Ensure-Tag")]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    public sealed class EnsureTagOperation : RemoteExecuteOperation
    {
        private readonly object progressLock = new();
        private RepoTransferProgress? currentProgress;

        [ScriptAlias("From")]
        [DisplayName("Repository connection")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitSecureResourceBase>))]
        public string? ResourceName { get; set; }

        [Required]
        [ScriptAlias("Tag")]
        public string? Tag { get; set; }

        [ScriptAlias("Commit")]
        [PlaceholderText("$Commit")]
        public string? Commit { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Username from repository connection")]
        public string? UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Password from repository connection")]
        [FieldEditMode(FieldEditMode.Password)]
        public string? Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("RepositoryUrl")]
        [DisplayName("Repository URL")]
        [PlaceholderText("Repository URL from repository connection")]
        public string? RepositoryUrl { get; set; }

        [Category("Advanced")]
        [ScriptAlias("Force")]
        [DisplayName("Force (overwrite)")]
        public bool Force { get; set; }

        public override OperationProgress? GetProgress()
        {
            RepoTransferProgress p;

            lock (this.progressLock)
            {
                if (!this.currentProgress.HasValue)
                    return null;

                p = this.currentProgress.GetValueOrDefault();
            }

            return new OperationProgress((int)(p.ReceivedObjects / (double)p.TotalObjects * 100), $"{p.ReceivedObjects}/{p.TotalObjects} objects received");
        }

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

            if (context.TryGetSecureResource(this.ResourceName, out var resource))
            {
                if (resource is not GitSecureResourceBase gitResource)
                    throw new ExecutionFailureException($"Invalid secure resource type ({resource.GetType().Name}); expected GitSecureResourceBase.");

                this.RepositoryUrl = await gitResource.GetRepositoryUrlAsync(context, context.CancellationToken);

                var credentials = resource.GetCredentials(context);
                if (credentials != null)
                {
                    if (credentials is not GitSecureCredentialsBase gitCredentials)
                        throw new ExecutionFailureException($"Invalid credential type ({credentials.GetType().Name}); expected GitSecureCredentialsBase.");

                    this.UserName = gitCredentials.UserName;
                    this.Password = AH.Unprotect(gitCredentials.Password);
                }
            }

            if (string.IsNullOrEmpty(this.RepositoryUrl))
                throw new ExecutionFailureException("RepositoryUrl was not specified and could not be determined using the repository connection.");
        }

        protected override async Task<object?> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var gitRepoRoot = Path.Combine(context.TempDirectory, ".gitrepos");

            this.LogInformation($"Updating local repository for {this.RepositoryUrl}...");

            using var repo = await RepoMan.FetchOrCloneAsync(
                new RepoManConfig(gitRepoRoot, new Uri(this.RepositoryUrl!), this.UserName, this.Password, this, this.HandleTransferProgress),
                context.CancellationToken
            );

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

        private void HandleTransferProgress(RepoTransferProgress transferProgress)
        {
            lock (this.progressLock)
            {
                this.currentProgress = transferProgress;
            }
        }
    }
}
