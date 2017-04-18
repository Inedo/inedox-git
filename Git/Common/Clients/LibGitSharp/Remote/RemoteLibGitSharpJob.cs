using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;

namespace Inedo.Extensions.Clients.LibGitSharp.Remote
{
    internal sealed class RemoteLibGitSharpJob : RemoteJob
    {
        public RemoteLibGitSharpJob()
        {
        }

        public ClientCommand Command { get; set; }
        public RemoteLibGitSharpContext Context { get; set; }

        public async override Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            var repo = new GitRepositoryInfo(
                new WorkspacePath(this.Context.LocalRepositoryPath),
                this.Context.RemoteRepositoryUrl,
                this.Context.UserName,
                this.Context.Password?.ToSecureString()
            );

            var client = new LibGitSharpClient(repo, this);
            
            switch (this.Command)
            {
                case ClientCommand.Archive:
                    await client.ArchiveAsync(this.Context.TargetDirectory).ConfigureAwait(false);
                    return null;

                case ClientCommand.Clone:
                    await client.CloneAsync(this.Context.CloneOptions).ConfigureAwait(false);
                    return null;

                case ClientCommand.EnumerateRemoteBranches:
                    return (await client.EnumerateRemoteBranchesAsync().ConfigureAwait(false)).ToArray();

                case ClientCommand.IsRepositoryValid:
                    return await client.IsRepositoryValidAsync().ConfigureAwait(false);

                case ClientCommand.Tag:
                    await client.TagAsync(this.Context.Tag, this.Context.Commit, this.Context.TagMessage);
                    return null;

                case ClientCommand.Update:
                    return await client.UpdateAsync(this.Context.UpdateOptions).ConfigureAwait(false);

                default:
                    throw new InvalidOperationException("Invalid remote LibGitSharp job type: " + this.Command);
            }
        }

        public override void Serialize(Stream stream)
        {
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);
            writer.Write((int)this.Command);
            new BinaryFormatter().Serialize(stream, this.Context);
        }

        public override void Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
            this.Command = (ClientCommand)reader.ReadInt32();
            this.Context = (RemoteLibGitSharpContext)new BinaryFormatter().Deserialize(stream);
        }

        public override void SerializeResponse(Stream stream, object result)
        {
            if (result != null)
                new BinaryFormatter().Serialize(stream, result);
        }

        public override object DeserializeResponse(Stream stream)
        {
            if (stream.Length > 0)
                return new BinaryFormatter().Deserialize(stream);
            else
                return null;
        }
    }
}
