namespace Inedo.Extensions.Git;

#nullable enable

internal sealed record class RepoExportOptions(string OutputDirectory, string Objectish, bool RecurseSubmodules, bool CreateSymbolicLinks, bool SetLastModified, bool WriteMinimalGitData);
