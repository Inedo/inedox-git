using System;
using System.Runtime.InteropServices;
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

            this.LocalRepositoryPath = localRepositoryPath.FullPath;
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
                uri.Password = this.Password.ToUnsecureString();
            }

            return uri.ToString();
        }
    }

#if Otter
    // remove this when BuildMaster SDK is updated to v5.7, and replace all SecureString extension methods with their AH equivalents
    internal static class SecureStringExtensions
    {
        public static string ToUnsecureString(this SecureString thisValue) => AH.Unprotect(thisValue);
        public static SecureString ToSecureString(this string s) => AH.CreateSecureString(s);
    }
#endif
}
