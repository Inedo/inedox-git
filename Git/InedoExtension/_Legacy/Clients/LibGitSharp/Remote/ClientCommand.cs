namespace Inedo.Extensions.Clients.LibGitSharp.Remote
{
    internal enum ClientCommand
    {
        Unknown,
        Archive,
        Clone,
        EnumerateRemoteBranches,
        IsRepositoryValid,
        Tag,
        Update,
        ListRepoFiles,
        GetFileLastModified
    }
}
