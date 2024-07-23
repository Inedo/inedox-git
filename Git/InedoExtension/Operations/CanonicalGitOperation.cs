using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Git;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;
using Inedo.Web;

#nullable enable

namespace Inedo.Extensions.Git.Operations
{
    public abstract class CanonicalGitOperation : RemoteExecuteOperation
    {
        private readonly object progressLock = new();
        private RepoTransferProgress? currentProgress;

        private protected CanonicalGitOperation()
        {
        }

        [DisplayIndex(-1)]
        [ScriptAlias("From")]
        [DisplayName("Repository connection")]
        [DefaultValue("$Repository")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitRepository>))]
        public string? ResourceName { get; set; }

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

        [Category("Connection/Identity")]
        [ScriptAlias("IgnoreCertificateCheck")]
        [DisplayName("Ignore Certificate Check")]
        [PlaceholderText("Use connection settings")]
        public bool IgnoreCertificateCheck { get; set; }

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

        private protected async Task EnsureCommonPropertiesAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrEmpty(this.RepositoryUrl))
            {
                if (string.IsNullOrWhiteSpace(this.ResourceName))
                    throw new ExecutionFailureException("Missing required argument: Repository (From)");

                if (!context.TryGetSecureResource(SecureResourceType.GitRepository, this.ResourceName, out var resource))
                    throw new ExecutionFailureException($"The repository named \"{this.ResourceName}\" could not be loaded.");

                if (resource is not GitRepository gitResource)
                    throw new ExecutionFailureException($"Invalid resource type ({resource.GetType().Name}); expected ${nameof(GitRepository)}.");

                await context.ExpandVariablesInPersistentPropertiesAsync(gitResource);

#pragma warning disable CS0618 // Type or member is obsolete
                this.RepositoryUrl = gitResource switch
                {
                    GenericGitRepository genericGit => genericGit.RepositoryUrl,
                    GitServiceRepository serviceGit => (await serviceGit.GetRepositoryInfoAsync(context, context.CancellationToken).ConfigureAwait(false)).RepositoryUrl,
                    GitSecureResourceBase legacyGit => await legacyGit.GetRepositoryUrlAsync(context, context.CancellationToken).ConfigureAwait(false),
                    _ => throw new ExecutionFailureException($"Unexpected resource type ({resource.GetType().Name}).")
                };
#pragma warning restore CS0618 // Type or member is obsolete

                var credentials = resource.GetCredentials(context);
                if (credentials != null)
                {
                     
                    if (credentials is GitServiceCredentials gitCredentials) 
                    {
                        this.UserName = gitCredentials.UserName;
                        this.Password = AH.Unprotect(gitCredentials.Password);

                        // SHIM FOR SDK3, where IgnoreCertificateCheck is a property
                        if (typeof(CanonicalGitOperation).Assembly.GetName().Version?.Major > 2)
                            throw new ExecutionFailureException("Remove IgnoreCertificateCheck");
                        if (gitCredentials.GetType().GetProperty("IgnoreCertificateCheck")?.GetValue(gitCredentials) is bool value && value)
                            this.IgnoreCertificateCheck = true;
                        // END SHIM

                        // if (gitCredentials.IgnoreCertificateCheck)
                        //     this.IgnoreCertificateCheck = true;

                    }
                    else if(credentials is UsernamePasswordCredentials usernamePasswordCredentials)
                    {
                        this.UserName = usernamePasswordCredentials.UserName;
                        this.Password = AH.Unprotect(usernamePasswordCredentials.Password);
                    }
                    else
                    {

                        throw new ExecutionFailureException($"Invalid credential type ({credentials.GetType().Name}); expected GitSecureCredentialsBase.");
                    }
                }

                if (string.IsNullOrEmpty(this.RepositoryUrl))
                    throw new ExecutionFailureException("RepositoryUrl was not specified and could not be determined using the repository connection.");

            }

            if (this.IgnoreCertificateCheck)
                this.LogInformation("IgnoreCertificateCheck has been set on the connection, which means that SSL/Certificate errors will be ignored.");
        }
        private protected async Task<RepoMan> FetchOrCloneAsync(IRemoteOperationExecutionContext context)
        {
            var gitRepoRoot = Path.Combine(context.TempDirectory, ".gitrepos");

            this.LogInformation($"Updating local repository for {this.RepositoryUrl}...");

            var repo = await RepoMan.FetchOrCloneAsync(
                new RepoManConfig(gitRepoRoot, new Uri(this.RepositoryUrl!), this.UserName, this.Password, this.IgnoreCertificateCheck, this, this.HandleTransferProgress),
                context.CancellationToken
            );

            lock (this.progressLock)
            {
                this.currentProgress = null;
            }

            return repo;
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
