namespace Inedo.Extensions.Git;

internal readonly record struct RepoTransferProgress(int TotalObjects, int ReceivedObjects, long ReceivedBytes);
