using System.ComponentModel;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Clients.CommandLine;
using Inedo.Extensions.Clients.LibGitSharp.Remote;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Operations
{
    public abstract class GitOperation : ExecuteOperation
    {
        [Category("Advanced")]
        [ScriptAlias("GitExePath")]
        [DisplayName("Git executable path")]
        [DefaultValue("$DefaultGitExePath")]
        public string GitExePath { get; set; }

        [Category("Advanced")]
        [ScriptAlias("RecurseSubmodules")]
        [DisplayName("Recurse submodules")]
        public bool RecurseSubmodules { get; set; }

        [Category("Advanced")]
        [ScriptAlias("WorkspaceDiskPath")]
        [DisplayName("Workspace disk path")]
        [PlaceholderText("Automatically managed")]
        [Description("If not set, a workspace name will be automatically generated and persisted based on the Repository URL or other host-specific information (e.g. GitHub's repository name).")]
        public string WorkspaceDiskPath { get; set; }

        [Category("Advanced")]
        [ScriptAlias("CleanWorkspace")]
        [DisplayName("Clean workspace")]
        [Description("If set to true, the workspace directory will be cleared before any Git-based operations are performed.")]
        public bool CleanWorkspace { get; set; }

        protected abstract UsernamePasswordCredentials GetCredentials();
        protected abstract Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken);

        protected GitClient CreateClient(IOperationExecutionContext context, string repositoryUrl, WorkspacePath workspacePath)
        {
            var creds = this.GetCredentials();

            if (!string.IsNullOrEmpty(this.GitExePath))
            {
                this.LogDebug($"Executable path specified, using Git command line client at \"{this.GitExePath}\"...");
                return new GitCommandLineClient(
                    this.GitExePath,
                    context.Agent.GetService<IRemoteProcessExecuter>(),
                    context.Agent.GetService<IFileOperationsExecuter>(),
                    new GitRepositoryInfo(workspacePath, repositoryUrl, creds?.UserName, creds?.Password),
                    this,
                    context.CancellationToken
                );
            }
            else
            {
                this.LogDebug("No executable path specified, using built-in Git library...");
                return new RemoteLibGitSharpClient(
                    context.Agent.TryGetService<IRemoteJobExecuter>(),
                    context.WorkingDirectory,
                    context.Simulation,
                    context.CancellationToken,
                    new GitRepositoryInfo(workspacePath, repositoryUrl, creds?.UserName, creds?.Password),
                    this
                );
            }
        }
    }
}
