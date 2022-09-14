using System.Security;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;

namespace Inedo.Extensions.Git.Legacy
{
    internal interface ILegacyGitOperation
    {
        string ResourceName { get; }

        protected GitSecureResourceBase GetResource(ICredentialResolutionContext context, string typeName, string assemblyName)
        {
            GitSecureResourceBase r;

            if (!string.IsNullOrWhiteSpace(this.ResourceName))
            {
                if (!context.TryGetSecureResource(this.ResourceName, out var resource) || resource is not GitSecureResourceBase gitResource)
                    throw new ExecutionFailureException("Invalid or missing git resource.");

                if (gitResource.GetType().FullName != typeName)
                    throw new ExecutionFailureException($"Invalid secure resource type (expected: {typeName}; actual: {gitResource.GetType().FullName})");

                r = gitResource;
            }
            else
            {
                r = CreateSecureResource(typeName, assemblyName);
            }

            return r;
        }

        protected static (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentials(GitSecureResourceBase gitResource, ICredentialResolutionContext context, string userName, SecureString password)
        {
            UsernamePasswordCredentials creds;

            if(!string.IsNullOrEmpty(userName) && password != null)
                creds = new UsernamePasswordCredentials { UserName = userName, Password = password };
            else
                creds = ((GitSecureCredentialsBase)gitResource.GetCredentials(context))?.ToUsernamePassword();

            return (creds, gitResource);
        }

        private static GitSecureResourceBase CreateSecureResource(string typeName, string assemblyName)
        {
            try
            {
                return (GitSecureResourceBase)Activator.CreateInstance(InedoExtensionsManager.GetPossiblyExtensibleType($"{typeName},{assemblyName}"));
            }
            catch
            {
                throw new ExecutionFailureException($"Secure resource was not specified and unable to create instance of {typeName}. The {assemblyName} extension may be missing.");
            }
        }
    }
}
