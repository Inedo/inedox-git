using System;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;

namespace Inedo.Extensions.Credentials
{
    public abstract class GitSecureResourceBase : SecureResource
    {
        public abstract Task<string> GetRepositoryUrl(ICredentialResolutionContext context, CancellationToken cancellationToken);
    }
    public abstract class GitSecureResourceBase<TCredentials> : GitSecureResourceBase
        where TCredentials : SecureCredentials
    {
        public override Type[] CompatibleCredentials => new[] { typeof(TCredentials) };
    }
    public abstract class GitSecureResourceBase<TCredentials1, TCredentials2> : GitSecureResourceBase
        where TCredentials1 : SecureCredentials
        where TCredentials2 : SecureCredentials
    {
        public override Type[] CompatibleCredentials => new[] { typeof(TCredentials1), typeof(TCredentials2) };
    }
}
