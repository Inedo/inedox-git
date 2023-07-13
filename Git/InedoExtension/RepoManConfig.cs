using Inedo.Diagnostics;
using LibGit2Sharp;

#nullable enable

namespace Inedo.Extensions.Git;

internal sealed record class RepoManConfig(string RootPath, Uri RepositoryUri, string? UserName = null, string? Password = null, ILogSink? Log = null, Action<RepoTransferProgress>? TransferProgress = null, SubmoduleInfo? Submodule = null)
{
    public LibGit2Sharp.Handlers.TransferProgressHandler? TransferProgressHandler => this.TransferProgress != null ? this.HandleTransferProgress : null;

#pragma warning disable IDE0060 // Remove unused parameter
    public LibGit2Sharp.Credentials GetCredentials(string u, string n, SupportedCredentialTypes t)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        return string.IsNullOrEmpty(this.UserName)
            ? new DefaultCredentials()
            : new UsernamePasswordCredentials { Username = this.UserName, Password = this.Password };
    }

    private bool HandleTransferProgress(TransferProgress p)
    {
        this.TransferProgress?.Invoke(new RepoTransferProgress(p.TotalObjects, p.ReceivedObjects, p.ReceivedBytes));
        return true;
    }
}
