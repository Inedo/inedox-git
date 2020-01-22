using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.AzureDevOps.Clients
{
    internal static class ArtifactImporter
    {
        public static async Task<string> DownloadAndImportAsync(SecureString token, string instanceUrl, ILogSink logger, string teamProject, string buildNumber, string buildDefinitionName, IOperationExecutionContext context, string artifactName)
        {
            var downloader = new ArtifactDownloader(token, instanceUrl, logger);

            using (var artifact = await downloader.DownloadAsync(teamProject, buildNumber, buildDefinitionName, artifactName).ConfigureAwait(false))
            {
                logger.LogInformation("Downloading artifact file from Azure DevOps and importing into BuildMaster artifact library...");

                var shim = new BuildMasterContextShim(context);

                await SDK.CreateArtifactAsync(
                    applicationId: shim.ApplicationId,
                    releaseNumber: shim.ReleaseNumber,
                    buildNumber: shim.BuildNumber,
                    deployableId: shim.DeployableId,
                    executionId: null,
                    artifactName: artifact.Name,
                    artifactData: artifact.Content,
                    overwrite: true
                ).ConfigureAwait(false);

                logger.LogInformation($"{artifact.Name} artifact imported.");

                return artifact.BuildNumber;
            }
        }

        private sealed class BuildMasterContextShim
        {
            private readonly object context;
            private readonly PropertyInfo[] properties;

            public BuildMasterContextShim(IOperationExecutionContext context)
            {
                // this is absolutely horrid, but works for backwards compatibility since this can only be used in BuildMaster
                this.context = context;
                this.properties = context.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            public int ApplicationId => int.Parse(this.GetValue());
            public int? DeployableId => AH.ParseInt(this.GetValue());
            public string ReleaseNumber => this.GetValue();
            public string BuildNumber => this.GetValue();

            private string GetValue([CallerMemberName] string name = null)
            {
                var prop = this.properties.FirstOrDefault(p => string.Equals(name, p.Name, StringComparison.OrdinalIgnoreCase));
                return prop?.GetValue(this.context)?.ToString();
            }
        }
    }
}
