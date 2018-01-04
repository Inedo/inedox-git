using Inedo.IO;
using System;
using System.Security;

namespace Inedo.Extensions.Clients
{
    public sealed class GitRepositoryInfo
    {
        public GitRepositoryInfo(WorkspacePath localRepositoryPath, string remoteRepositoryUrl, string userName, SecureString password)
        {
            if (string.IsNullOrEmpty(localRepositoryPath?.FullPath))
                throw new ArgumentNullException(nameof(localRepositoryPath));
            if (string.IsNullOrEmpty(remoteRepositoryUrl))
                throw new ArgumentNullException(nameof(remoteRepositoryUrl));

            this.LocalRepositoryPath = PathEx.EnsureTrailingDirectorySeparator(localRepositoryPath.FullPath);
            this.RemoteRepositoryUrl = remoteRepositoryUrl;
            this.UserName = userName;
            this.Password = password;
        }

        public string LocalRepositoryPath { get; }
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
