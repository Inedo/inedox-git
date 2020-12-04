using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Serialization;

namespace Inedo.Extensions.Clients.LibGitSharp.Remote
{
    internal sealed class RemoteLibGitSharpJob : RemoteJob, ILogSink
    {
        public RemoteLibGitSharpJob()
        {
        }

        public ClientCommand Command { get; set; }
        public RemoteLibGitSharpContext Context { get; set; }

        public async override Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            GitRepositoryInfo repo;

            if (!string.IsNullOrEmpty(this.Context.LocalRepositoryPath))
            {
                repo = new GitRepositoryInfo(
                    new WorkspacePath(this.Context.LocalRepositoryPath),
                    this.Context.RemoteRepositoryUrl,
                    this.Context.UserName,
                    AH.CreateSecureString(this.Context.Password)
                );
            }
            else
            {
                repo = new GitRepositoryInfo(
                    this.Context.RemoteRepositoryUrl,
                    this.Context.UserName,
                    AH.CreateSecureString(this.Context.Password)
                );
            }

            var client = new LibGitSharpClient(repo, this);
            
            switch (this.Command)
            {
                case ClientCommand.Archive:
                    await client.ArchiveAsync(this.Context.TargetDirectory, this.Context.KeepInternals).ConfigureAwait(false);
                    return null;

                case ClientCommand.Clone:
                    await client.CloneAsync(this.Context.CloneOptions).ConfigureAwait(false);
                    return null;

                case ClientCommand.EnumerateRemoteBranches:
                    return (await client.EnumerateRemoteBranchesAsync().ConfigureAwait(false)).ToArray();

                case ClientCommand.IsRepositoryValid:
                    return await client.IsRepositoryValidAsync().ConfigureAwait(false);

                case ClientCommand.Tag:
                    await client.TagAsync(this.Context.Tag, this.Context.Commit, this.Context.TagMessage, this.Context.Force).ConfigureAwait(false);
                    return null;

                case ClientCommand.Update:
                    return await client.UpdateAsync(this.Context.UpdateOptions).ConfigureAwait(false);

                case ClientCommand.ListRepoFiles:
                    return (await client.ListRepoFilesAsync().ConfigureAwait(false)).ToArray();

                case ClientCommand.GetFileLastModified:
                    return await client.GetFileLastModifiedAsync(this.Context.FileName).ConfigureAwait(false);

                default:
                    throw new InvalidOperationException("Invalid remote LibGitSharp job type: " + this.Command);
            }
        }

        public override void Serialize(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding, true))
            {
                writer.Write((int)this.Command);
            }

            SlimBinaryFormatter.Serialize(this.Context, stream);
        }

        public override void Deserialize(Stream stream)
        {
            using (var reader = new BinaryReader(stream, InedoLib.UTF8Encoding, true))
            {
                this.Command = (ClientCommand)reader.ReadInt32();
            }

            this.Context = (RemoteLibGitSharpContext)SlimBinaryFormatter.Deserialize(stream);
        }

        public override void SerializeResponse(Stream stream, object result) => SlimBinaryFormatter.Serialize(result, stream);
        public override object DeserializeResponse(Stream stream) => SlimBinaryFormatter.Deserialize(stream);

        void ILogSink.Log(IMessage message) => this.Log(message.Level, message.Message);
    }
}
