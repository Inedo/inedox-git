using System;
using System.Security;
using Inedo.IO;

namespace Inedo.Extensions.Clients
{
    public sealed class GitRepositoryInfo
    {
        private readonly string localRepositoryPath;

        public GitRepositoryInfo(WorkspacePath localRepositoryPath, string remoteRepositoryUrl, string userName, SecureString password)
        {
            if (string.IsNullOrEmpty(localRepositoryPath?.FullPath))
                throw new ArgumentNullException(nameof(localRepositoryPath));
            if (string.IsNullOrEmpty(remoteRepositoryUrl))
                throw new ArgumentNullException(nameof(remoteRepositoryUrl));

            this.localRepositoryPath = PathEx.EnsureTrailingDirectorySeparator(localRepositoryPath.FullPath);
            this.RemoteRepositoryUrl = remoteRepositoryUrl;
            this.UserName = userName;
            this.Password = password;
        }
        public GitRepositoryInfo(string remoteRepositoryUrl, string userName, SecureString password)
        {
            if (string.IsNullOrEmpty(remoteRepositoryUrl))
                throw new ArgumentNullException(nameof(remoteRepositoryUrl));

            this.RemoteRepositoryUrl = remoteRepositoryUrl;
            this.UserName = userName;
            this.Password = password;
        }

        public bool HasLocalRepository => this.localRepositoryPath != null;
        public string LocalRepositoryPath => this.localRepositoryPath ?? throw new InvalidOperationException("This operation requires a local repository path.");
        public string RemoteRepositoryUrl { get; }
        public string UserName { get; }
        public SecureString Password { get; }

        public string GetRemoteUrlWithCredentials()
        {
            var uri = new UriBuilder(this.RemoteRepositoryUrl);
            if (!string.IsNullOrEmpty(this.UserName))
            {
                uri.UserName = this.UserName;
                uri.Password = AH.Unprotect(this.Password);
            }

            return uri.ToString();
        }
    }
}
