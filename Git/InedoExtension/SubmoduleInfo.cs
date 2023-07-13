namespace Inedo.Extensions.Git;

#nullable enable

internal sealed record class SubmoduleInfo(string Name, string Path, string Url, string? CommitSha = null);
