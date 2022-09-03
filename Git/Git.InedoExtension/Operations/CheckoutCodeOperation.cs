using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials.Git;
using Inedo.Web;

#nullable enable

namespace Inedo.Extensions.Git.Operations
{
    [DisplayName("Check Out Code from Git")]
    [Description("Gets source code from a git repository.")]
    [Tag("git")]
    [Tag("source-control")]
    [ScriptAlias("Checkout-Code")]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    public sealed class CheckoutCodeOperation : RemoteExecuteOperation
    {
        private readonly object progressLock = new();
        private RepoTransferProgress? currentProgress;

        [ScriptAlias("From")]
        [DisplayName("Repository connection")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitSecureResourceBase>))]
        public string? ResourceName { get; set; }

        [ScriptAlias("To")]
        [PlaceholderText("$WorkingDirectory")]
        public string? OutputDirectory { get; set; }

#warning this name is rubbish
        [ScriptAlias("BranchOrCommit")]
        [PlaceholderText("default")]
        public string? Objectish { get; set; }

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

        protected override Task<object?> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var gitRepoRoot = Path.Combine(context.TempDirectory, ".gitrepos");
            var outputDirectory = context.ResolvePath(this.OutputDirectory);

            using var repo = RepoMan.FetchOrClone(gitRepoRoot, new Uri(this.RepositoryUrl!), this.UserName, this.Password, this, this.HandleTransferProgress);

            lock (this.progressLock)
            {
                this.currentProgress = null;
            }

            repo.Export(outputDirectory, this.Objectish!, this.RecurseSubmodules);
            return Task.FromResult<object?>(repo.GetCommitHash(this.Objectish!));
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

        private void HandleTransferProgress(RepoTransferProgress transferProgress)
        {
            lock (this.progressLock)
            {
                this.currentProgress = transferProgress;
            }
        }
    }
}