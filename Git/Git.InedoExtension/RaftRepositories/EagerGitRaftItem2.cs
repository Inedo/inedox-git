using System;
using System.IO;
using System.Text;
using Inedo.Extensibility.RaftRepositories;
using LibGit2Sharp;

namespace Inedo.Extensions.Git.RaftRepositories
{
    internal sealed class EagerGitRaftItem2 : RaftItem2
    {
        private readonly byte[] data;

        public EagerGitRaftItem2(RaftItemType type, TreeEntry treeEntry, GitRaftRepository2 raft, Commit commit = null, bool useCommitCache = false)
            : base(type, treeEntry.Name)
        {
            if (treeEntry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)treeEntry.Target;
                using var blobStream = blob.GetContentStream();
                using var memoryStream = new MemoryStream();
                blobStream.CopyTo(memoryStream);
                this.data = memoryStream.ToArray();
            }

            var itemCommit = commit ?? raft.GetLatestCommit(treeEntry.Path, useCommitCache);
            if (itemCommit != null)
            {
                this.LastWriteTime = itemCommit.Committer?.When ?? DateTimeOffset.Now;
                this.ModifiedByUser = itemCommit.Committer?.Name;
                this.ItemVersion = itemCommit.Sha;
            }
        }

        public override DateTimeOffset LastWriteTime { get; }
        public override string ItemVersion { get; }
        public override string ModifiedByUser { get; }
        public override long? ItemSize => this.data?.Length ?? 0;

        public override Stream OpenRead() => new MemoryStream(this.data ?? new byte[0], false);
        public override TextReader OpenTextReader() => new StreamReader(this.OpenRead(), Encoding.UTF8);
        public override byte[] ReadAllBytes() => this.data ?? new byte[0];
        public override string ReadAllText() => Encoding.UTF8.GetString(this.data ?? new byte[0]);
    }
}
