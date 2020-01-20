using Inedo.Extensibility.Credentials;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Credentials
{
    public abstract class GitSecureCredentialsBase : SecureCredentials
    {
        public abstract UsernamePasswordCredentials ToUsernamePassword();
        public static implicit operator UsernamePasswordCredentials(GitSecureCredentialsBase id) => id.ToUsernamePassword();
    }
}
