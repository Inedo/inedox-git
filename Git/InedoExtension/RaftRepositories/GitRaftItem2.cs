using Inedo.Extensibility.RaftRepositories;
using LibGit2Sharp;

namespace Inedo.Extensions.Git.RaftRepositories;

internal sealed class GitRaftItem2 : RaftItem2
{
    private readonly Commit commit;
    private readonly Lazy<byte[]> content;

    public GitRaftItem2(RaftItemType type, string name, GitObject target, Commit commit)
        : base(type, name)
    {
        this.commit = commit;
        this.content = new Lazy<byte[]>(() => ReadContent(target));
    }

    public override DateTimeOffset LastWriteTime => this.commit.Author.When;
    public override string ModifiedByUser => this.commit.Author.Name;
    public override string ItemVersion => this.commit.Sha;
    public override long? ItemSize => this.content.Value.Length;

    public override Stream OpenRead() => new MemoryStream(this.content.Value, false);
    public override TextReader OpenTextReader() => new StreamReader(this.OpenRead(), InedoLib.UTF8Encoding);
    public override byte[] ReadAllBytes() => this.content.Value;
    public override string ReadAllText() => InedoLib.UTF8Encoding.GetString(this.content.Value);

    private static byte[] ReadContent(GitObject target)
    {
        if (target is not Blob blob)
            return Array.Empty<byte>();

        using var stream = blob.GetContentStream();
        if (stream.CanSeek)
        {
            var data = new byte[stream.Length];
            var remaining = data.AsSpan();
            while (!remaining.IsEmpty)
            {
                int read = stream.Read(remaining);
                if (read == 0)
                    break;
                remaining = remaining[read..];
            }

            return data;
        }
        else
        {
            using var temp = new MemoryStream();
            stream.CopyTo(temp);
            return temp.ToArray();
        }
    }
}
