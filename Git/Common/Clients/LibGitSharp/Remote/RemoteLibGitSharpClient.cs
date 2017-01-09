using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using System.Threading;

#if BuildMaster
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Clients.LibGitSharp.Remote
{
    public sealed class RemoteLibGitSharpClient : GitClient
    {
        private IRemoteJobExecuter jobExecuter;
        private string workingDirectory;
        private bool simulation;
        private CancellationToken cancellationToken;

        public RemoteLibGitSharpClient(IRemoteJobExecuter jobExecuter, string workingDirectory, bool simulation, CancellationToken cancellationToken, GitRepositoryInfo repository, ILogger log) 
            : base(repository, log)
        {
            if (jobExecuter == null)
                throw new NotSupportedException("A hosted agent must be used with the built-in LibGitSharp git client.");
            if (workingDirectory == null)
                throw new ArgumentNullException(nameof(WorkspacePath));

            this.jobExecuter = jobExecuter;
            this.workingDirectory = workingDirectory;
            this.simulation = simulation;
            this.cancellationToken = cancellationToken;
        }

        public async override Task ArchiveAsync(string targetDirectory)
        {
            await this.ExecuteRemoteAsync(
                ClientCommand.Archive, 
                new RemoteLibGitSharpContext { TargetDirectory = targetDirectory }
            ).ConfigureAwait(false);
        }

        public async override Task CloneAsync(GitCloneOptions options)
        {
            await this.ExecuteRemoteAsync(
                ClientCommand.Clone, 
                new RemoteLibGitSharpContext { CloneOptions = options }
            ).ConfigureAwait(false);
        }

        public async override Task<IEnumerable<string>> EnumerateRemoteBranchesAsync()
        {
            var result = await this.ExecuteRemoteAsync(
                ClientCommand.EnumerateRemoteBranches,
                new RemoteLibGitSharpContext()
            ).ConfigureAwait(false);

            return (IEnumerable<string>)result;
        }

        public async override Task<bool> IsRepositoryValidAsync()
        {
            var result = await this.ExecuteRemoteAsync(
                ClientCommand.IsRepositoryValid,
                new RemoteLibGitSharpContext()
            ).ConfigureAwait(false);

            return (bool)result;
        }

        public async override Task TagAsync(string tag)
        {
            await this.ExecuteRemoteAsync(
                ClientCommand.Tag,
                new RemoteLibGitSharpContext { Tag = tag }
            ).ConfigureAwait(false);
        }

        public async override Task UpdateAsync(GitUpdateOptions options)
        {
            await this.ExecuteRemoteAsync(
                ClientCommand.Update,
                new RemoteLibGitSharpContext { UpdateOptions = options }
            ).ConfigureAwait(false);
        }

        private async Task<object> ExecuteRemoteAsync(ClientCommand command, RemoteLibGitSharpContext context)
        {
            context.WorkingDirectory = this.workingDirectory;
            context.Simulation = this.simulation;
            context.LocalRepositoryPath = this.repository.LocalRepositoryPath;
            context.RemoteRepositoryUrl = this.repository.RemoteRepositoryUrl;
            context.UserName = this.repository.UserName;
            context.Password = this.repository.Password?.ToUnsecureString();

            var job = new RemoteLibGitSharpJob();
            job.MessageLogged += Job_MessageLogged;
            job.Command = command;
            job.Context = context;

            var result = await this.jobExecuter.ExecuteJobAsync(job, this.cancellationToken).ConfigureAwait(false);
            return result;
        }

        private void Job_MessageLogged(object sender, LogMessageEventArgs e)
        {
            this.log.Log(e.Level, e.Message);
        }
    }
}
