using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;

namespace Inedo.Extensions.AzureDevOps
{
    internal interface IAzureDevOpsConfiguration
    {
        string ResourceName { get; }
        string InstanceUrl { get; }
        string UserName { get; set; }
        SecureString Token { get; }
        string ProjectName { get; }
        string RepositoryName { get; }
    }

    internal static class AzureDevOpsOperationExtensions
    {
        public static string DescribeSource(this IOperationConfiguration config)
        {
            return AH.CoalesceString(
                config[nameof(IAzureDevOpsConfiguration.RepositoryName)],
                config[nameof(IAzureDevOpsConfiguration.ProjectName)],
                config[nameof(IAzureDevOpsConfiguration.ResourceName)],
                "(unknown)");
        }
        public static (AzureDevOpsAccount, AzureDevOpsRepository) GetCredentialsAndResource(this IAzureDevOpsConfiguration operation, IOperationExecutionContext context)
            => GetCredentialsAndResource(operation, (ICredentialResolutionContext)context);
        public static (AzureDevOpsAccount, AzureDevOpsRepository) GetCredentialsAndResource(this IAzureDevOpsConfiguration operation, ICredentialResolutionContext context)
        {
            AzureDevOpsAccount credentials;
            AzureDevOpsRepository resource;
            if (string.IsNullOrEmpty(operation.ResourceName))
            {
                credentials = operation.Token == null ? null : new AzureDevOpsAccount();
                resource = string.IsNullOrEmpty(operation.InstanceUrl) ? null : new AzureDevOpsRepository();
            }
            else
            {
                resource = (AzureDevOpsRepository)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                    credentials = null;
                else
                    credentials = (AzureDevOpsAccount)resource.GetCredentials(context);
            }

            if (credentials != null)
            {
                credentials.UserName = AH.CoalesceString(operation.UserName, credentials.UserName);
                credentials.Password = operation.Token ?? credentials.Password;
            }
            if (resource != null)
            {
                resource.LegacyInstanceUrl = AH.CoalesceString(operation.InstanceUrl, resource.LegacyInstanceUrl);
                resource.RepositoryName = AH.CoalesceString(operation.RepositoryName, resource.RepositoryName);
                resource.ProjectName = AH.CoalesceString(operation.ProjectName, resource.ProjectName);
            }

            return (credentials, resource);
        }
    }
}

