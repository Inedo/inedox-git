using Inedo.Diagnostics;

#nullable enable

namespace Inedo.Extensions.Git;

internal sealed record class RepoManConfig(string RootPath, Uri RepositoryUri, string? UserName = null, string? Password = null, bool IgnoreCertificateCheck = false, ILogSink? Log = null, Action<RepoTransferProgress>? TransferProgress = null, SubmoduleInfo? Submodule = null);
