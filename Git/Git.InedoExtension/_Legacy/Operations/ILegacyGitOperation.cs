using System.Security;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;

namespace Inedo.Extensions.Git.Legacy
{
    internal static class LegacyGitOperationExtensions
    {
        public static UsernamePasswordCredentials ToUsernamePassword(this GitServiceCredentials credentials)
        {
            return new UsernamePasswordCredentials
            {
                UserName = credentials.UserName,
                Password = credentials.Password
            };
        }
    }
    [Obsolete]
    internal interface ILegacyGitOperation
    {
        string ResourceName { get; }

        protected GitRepository GetResource(ICredentialResolutionContext context, string typeName, string assemblyName)
        {
            GitRepository r;

            if (!string.IsNullOrWhiteSpace(this.ResourceName))
            {
                if (!context.TryGetSecureResource(this.ResourceName, out var resource) || resource is not GitRepository gitResource)
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

        protected static (UsernamePasswordCredentials, GitRepository) GetCredentials(GitRepository gitResource, ICredentialResolutionContext context, string userName, SecureString password)
        {
            UsernamePasswordCredentials creds;

            if(!string.IsNullOrEmpty(userName) && password != null)
                creds = new UsernamePasswordCredentials { UserName = userName, Password = password };
            else
                creds = ((GitServiceCredentials)gitResource.GetCredentials(context))?.ToUsernamePassword();

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
