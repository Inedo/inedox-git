using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensions.AzureDevOps.Clients.Rest;

namespace Inedo.Extensions.AzureDevOps.Clients
{
    internal sealed class ArtifactDownloader
    {
        private readonly ILogSink logger;
        private readonly SecureString token;
        private readonly string instanceUrl;

        public ArtifactDownloader(SecureString token, string instanceUrl, ILogSink log)
        {
            if (string.IsNullOrEmpty(instanceUrl))
                throw new InvalidOperationException("The base URL property of the AzureDevOps credentials or the Url property of the Import/Download Azure DevOps Artifact operation must be set.");

            this.token = token;
            this.instanceUrl = instanceUrl;
            this.logger = log;
        }

        public async Task<AzureDevOpsArtifact> DownloadAsync(string teamProject, string buildNumber, string buildDefinitionName, string artifactName)
        {
            if (string.IsNullOrEmpty(teamProject))
                throw new ArgumentException("A project is required to download the artifact.", nameof(teamProject));
            if (string.IsNullOrEmpty(artifactName))
                throw new ArgumentException("An artifact name is required to download the artifact.", nameof(artifactName));

            var api = new RestApi(this.token, this.instanceUrl, this.logger);

            var buildDefinition = await api.GetBuildDefinitionAsync(teamProject, buildDefinitionName).ConfigureAwait(false);
            if (buildDefinition == null)
                throw new InvalidOperationException($"The build definition {buildDefinitionName} could not be found.");

            this.logger?.LogInformation($"Finding {AH.CoalesceString(buildNumber, "last successful")} build...");

            var builds = await api.GetBuildsAsync(
                project: teamProject,
                buildDefinition: buildDefinition.id,
                buildNumber: AH.NullIf(buildNumber, ""),
                resultFilter: "succeeded",
                statusFilter: "completed",
                top: 2
            ).ConfigureAwait(false);

            if (builds.Length == 0)
                throw new InvalidOperationException($"Could not find build number {buildNumber}. Ensure there is a successful, completed build with this number.");

            var build = builds.FirstOrDefault();
            
            this.logger?.LogInformation($"Downloading {artifactName} artifact from Azure DevOps...");

            var stream = await api.DownloadArtifactAsync(teamProject, build.id, artifactName).ConfigureAwait(false);

            return new AzureDevOpsArtifact(stream, artifactName, buildNumber);
        }
    }

    internal sealed class AzureDevOpsArtifact : IDisposable
    {
        public AzureDevOpsArtifact(Stream content, string name, string buildNumber)
        {
            this.Content = content;
            this.Name = name;
            this.BuildNumber = buildNumber;
        }

        public Stream Content { get; }
        public string Name { get; }
        public string BuildNumber { get; }
        public string FileName => this.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? this.Name : (this.Name + ".zip");

        public void Dispose() => this.Content?.Dispose();
    }
}
