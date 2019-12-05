using System;
using System.IO;
using System.Linq;
using System.Text;
using Inedo.Extensibility.RaftRepositories;
using LibGit2Sharp;

namespace Inedo.Extensions.Git.RaftRepositories
{
    internal sealed class GitRaftItem2 : RaftItem2
    {
        private readonly TreeEntry treeEntry;
        private readonly Lazy<Commit> latestCommit;
        private readonly GitRaftRepository2 raft;
        private readonly Commit explicitCommit;

        public GitRaftItem2(RaftItemType type, TreeEntry treeEntry, GitRaftRepository2 raft, Commit commit = null)
            : base(type, treeEntry.Name)
        {
            this.latestCommit = new Lazy<Commit>(this.GetLatestCommit);
            this.treeEntry = treeEntry;
            this.raft = raft;
            this.explicitCommit = commit;
        }

        public override DateTimeOffset LastWriteTime => this.Commit?.Committer?.When ?? DateTimeOffset.Now;
        public override string ItemVersion => this.Commit?.Sha;
        public override string ModifiedByUser => this.Commit?.Committer?.Name;
        public override long? ItemSize => this.raft.Repo.ObjectDatabase.RetrieveObjectMetadata(this.treeEntry.Target.Id)?.Size;

        private Commit Commit => this.explicitCommit ?? this.latestCommit.Value;

        public override Stream OpenRead()
        {
            var blob = this.GetBlob();
            return blob?.GetContentStream();
        }
        public override TextReader OpenTextReader() => new StringReader(this.ReadAllText() ?? string.Empty);
        public override byte[] ReadAllBytes()
        {
            using (var stream = this.OpenRead())
            {
                if (stream == null)
                    return null;

                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    return buffer.ToArray();
                }
            }
        }
        public override string ReadAllText()
        {
            var blob = this.GetBlob();
            return blob?.GetContentText(Encoding.UTF8);
        }

        private Commit GetLatestCommit()
        {
            var commits = this.raft.Repo.Commits.QueryBy(this.treeEntry.Path,
                new CommitFilter
                {
                    IncludeReachableFrom = this.raft.Repo.Branches[this.raft.CurrentBranchName],
                    FirstParentOnly = false,
                    SortBy = CommitSortStrategies.Time,
                }
            );

            return commits.FirstOrDefault()?.Commit;
        }
        private Blob GetBlob() => this.treeEntry.TargetType == TreeEntryTargetType.Blob ? (Blob)this.treeEntry.Target : null;
    }
}
