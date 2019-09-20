using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Clients.LibGitSharp.Remote
{
    public sealed class RemoteLibGitSharpClient : GitClient
    {
        private IRemoteJobExecuter jobExecuter;
        private string workingDirectory;
        private bool simulation;
        private CancellationToken cancellationToken;

        public RemoteLibGitSharpClient(IRemoteJobExecuter jobExecuter, string workingDirectory, bool simulation, CancellationToken cancellationToken, GitRepositoryInfo repository, ILogSink log) 
            : base(repository, log)
        {
            this.jobExecuter = jobExecuter ?? throw new NotSupportedException("A hosted agent must be used with the built-in LibGitSharp git client.");
            this.workingDirectory = workingDirectory;
            this.simulation = simulation;
            this.cancellationToken = cancellationToken;
        }

        public override Task ArchiveAsync(string targetDirectory, bool keepInternals = false)
        {
            return this.ExecuteRemoteAsync(
                ClientCommand.Archive,
                new RemoteLibGitSharpContext { TargetDirectory = targetDirectory, KeepInternals = keepInternals }
            );
        }

        public override Task CloneAsync(GitCloneOptions options)
        {
            return this.ExecuteRemoteAsync(
                ClientCommand.Clone,
                new RemoteLibGitSharpContext { CloneOptions = options }
            );
        }

        public async override Task<IEnumerable<RemoteBranchInfo>> EnumerateRemoteBranchesAsync()
        {
            var result = await this.ExecuteRemoteAsync(
                ClientCommand.EnumerateRemoteBranches,
                new RemoteLibGitSharpContext()
            ).ConfigureAwait(false);

            return (IEnumerable<RemoteBranchInfo>)result;
        }

        public async override Task<bool> IsRepositoryValidAsync()
        {
            var result = await this.ExecuteRemoteAsync(
                ClientCommand.IsRepositoryValid,
                new RemoteLibGitSharpContext()
            ).ConfigureAwait(false);

            return (bool)result;
        }

        public override Task TagAsync(string tag, string commit, string message, bool force = false)
        {
            return this.ExecuteRemoteAsync(
                ClientCommand.Tag,
                new RemoteLibGitSharpContext { Tag = tag, Commit = commit, TagMessage = message, Force = force }
            );
        }

        public async override Task<string> UpdateAsync(GitUpdateOptions options)
        {
            return (string)await this.ExecuteRemoteAsync(
                ClientCommand.Update,
                new RemoteLibGitSharpContext { UpdateOptions = options }
            ).ConfigureAwait(false);
        }

        public override async Task<IReadOnlyList<string>> ListRepoFilesAsync()
        {
            return (string[])await this.ExecuteRemoteAsync(
                ClientCommand.ListRepoFiles,
                new RemoteLibGitSharpContext()
            ).ConfigureAwait(false);
        }

        public override async Task<DateTimeOffset?> GetFileLastModifiedAsync(string fileName)
        {
            return (DateTimeOffset?)await this.ExecuteRemoteAsync(
                ClientCommand.GetFileLastModified,
                new RemoteLibGitSharpContext { FileName = fileName }
            ).ConfigureAwait(false);
        }

        private async Task<object> ExecuteRemoteAsync(ClientCommand command, RemoteLibGitSharpContext context)
        {
            context.WorkingDirectory = this.workingDirectory;
            context.Simulation = this.simulation;
            context.LocalRepositoryPath = this.repository.HasLocalRepository ? this.repository.LocalRepositoryPath : null;
            context.RemoteRepositoryUrl = this.repository.RemoteRepositoryUrl;
            context.UserName = this.repository.UserName;
            context.Password = AH.Unprotect(this.repository.Password);

            var job = new RemoteLibGitSharpJob();
            job.MessageLogged += this.Job_MessageLogged;
            job.Command = command;
            job.Context = context;

            var result = await this.jobExecuter.ExecuteJobAsync(job, this.cancellationToken).ConfigureAwait(false);
            return result;
        }

        private void Job_MessageLogged(object sender, LogMessageEventArgs e) => this.log.Log(e.Level, e.Message);
    }
}
