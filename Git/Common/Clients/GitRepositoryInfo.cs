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
    internal static class SecureStringExtensions
    {
        public static string ToUnsecureString(this SecureString thisValue)
        {
            if (thisValue == null)
                throw new ArgumentNullException(nameof(thisValue));

            var str = IntPtr.Zero;
            try
            {
                str = Marshal.SecureStringToGlobalAllocUnicode(thisValue);
                return Marshal.PtrToStringUni(str);
            }
            finally
            {
                if (str != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(str);
            }
        }

        public static SecureString ToSecureString(this string s)
        {
            if (s == null)
                return null;

            var secure = new SecureString();
            foreach (var c in s)
                secure.AppendChar(c);

            secure.MakeReadOnly();
            return secure;
        }
    }
#endif
}
