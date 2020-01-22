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
        public GitRaftItem2(RaftItemType type, TreeEntry treeEntry, GitRaftRepository2 raft, Commit commit = null)
            : base(type, treeEntry.Name)
        {
            if (commit == null)
            {
                var commits = raft.Repo.Commits.QueryBy(treeEntry.Path,
                    new CommitFilter
                    {
                        IncludeReachableFrom = raft.Repo.Branches[raft.CurrentBranchName],
                        FirstParentOnly = false,
                        SortBy = CommitSortStrategies.Time,
                    }
                );

                commit = commits.FirstOrDefault()?.Commit;
            }

            this.LastWriteTime = commit.Committer?.When ?? DateTimeOffset.Now;
            this.ItemVersion = commit.Sha;
            this.ModifiedByUser = commit.Committer?.Name;
            this.ItemSize = raft.Repo.ObjectDatabase.RetrieveObjectMetadata(treeEntry.Target.Id)?.Size;

            if (treeEntry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)treeEntry.Target;

                using (var temp = new MemoryStream((int)blob.Size))
                {
                    using (var stream = blob.GetContentStream())
                    {
                        stream.CopyTo(temp);
                    }

                    this.Data = temp.ToArray();
                }
            }
        }

        public override DateTimeOffset LastWriteTime { get; }
        public override string ItemVersion { get; }
        public override string ModifiedByUser { get; }
        public override long? ItemSize { get; }

        private byte[] Data { get; }

        public override Stream OpenRead() => new MemoryStream(this.Data, false);
        public override TextReader OpenTextReader() => new StringReader(this.ReadAllText());
        public override byte[] ReadAllBytes() => (byte[])this.Data?.Clone();
        public override string ReadAllText() => Encoding.UTF8.GetString(this.Data);
    }
}
