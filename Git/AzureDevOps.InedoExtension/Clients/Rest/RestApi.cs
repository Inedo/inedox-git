using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensions.AzureDevOps.VisualStudioOnline.Model;
using Newtonsoft.Json;

namespace Inedo.Extensions.AzureDevOps.Clients.Rest
{
    internal sealed class QueryString
    {
        public static QueryString Default = new QueryString();

        public string ApiVersion { get; set; } = "2.0";
        public string Expand { get; set; }

        public string BuildNumber { get; set; }
        public int Definition { get; set; }
        public int? Top { get; set; }
        public string ResultFilter { get; set; }
        public string StatusFilter { get; set; }

        public IEnumerable<int> Ids { get; set; }
        public string Timeframe { get; set; }

        public override string ToString()
        {
            var buffer = new StringBuilder(1024);
            buffer.Append('?');
            if (this.ApiVersion != null)
                buffer.AppendFormat("api-version={0}&", this.ApiVersion);
            if (this.BuildNumber != null)
                buffer.AppendFormat("buildNumber={0}&", this.BuildNumber);
            if (this.Definition != 0)
                buffer.AppendFormat("definitions={0}&", this.Definition);
            if (this.Top != null)
                buffer.AppendFormat("$top={0}&", this.Top);
            if (this.ResultFilter != null)
                buffer.AppendFormat("resultFilter={0}&", this.ResultFilter);
            if (this.StatusFilter != null)
                buffer.AppendFormat("statusFilter={0}&", this.StatusFilter);
            if (this.Ids != null)
                buffer.AppendFormat("ids={0}&", string.Join(",", this.Ids));
            if (this.Timeframe != null)
                buffer.AppendFormat("$timeframe={0}&", this.Timeframe);
            if (this.Expand != null)
                buffer.AppendFormat("$expand={0}&", this.Expand);

            return buffer.ToString().TrimEnd('?', '&');
        }
    }

    internal sealed class RestApi
    {
        private readonly ILogSink log;
        private readonly SecureString token;
        private readonly string instanceUrl;

        public RestApi(SecureString token, string instanceUrl, ILogSink log)
        {
            this.token = token;
            this.instanceUrl = instanceUrl;
            this.log = log;
        }

        public async Task<GetWorkItemResponse> CreateWorkItemAsync(string project, string workItemType, string title, string description, string iterationPath)
        {
            var args = new List<object>(8);
            args.Add(new { op = "add", path = "/fields/System.Title", value = title });
            if (!string.IsNullOrEmpty(description))
                args.Add(new { op = "add", path = "/fields/System.Description", value = description });
            if (!string.IsNullOrEmpty(iterationPath))
                args.Add(new { op = "add", path = "/fields/System.IterationPath", value = iterationPath });

            var response = await this.InvokeAsync<GetWorkItemResponse>(
                "PATCH",
                project,
                $"wit/workitems/${Uri.EscapeDataString(workItemType)}",
                QueryString.Default,
                args.ToArray(),
                contentType: "application/json-patch+json"
            ).ConfigureAwait(false);

            return response;
        }

        public async Task<GetWorkItemResponse> UpdateWorkItemAsync(string id, string title, string description, string iterationPath, string state)
        {
            var args = new List<object>(8);

            // have to use "remove" then "add" because "replace" errors out if a value is missing
            if (!string.IsNullOrEmpty(title))
            {
                args.Add(new { op = "remove", path = "/fields/System.Title" });
                args.Add(new { op = "add", path = "/fields/System.Title", value = title });
            }
            if (!string.IsNullOrEmpty(description))
            {
                args.Add(new { op = "remove", path = "/fields/System.Description" });
                args.Add(new { op = "add", path = "/fields/System.Description", value = description });
            }
            if (!string.IsNullOrEmpty(iterationPath))
            {
                args.Add(new { op = "remove", path = "/fields/System.IterationPath" });
                args.Add(new { op = "add", path = "/fields/System.IterationPath", value = iterationPath });
            }
            if (!string.IsNullOrEmpty(state))
            {
                args.Add(new { op = "remove", path = "/fields/System.State" });
                args.Add(new { op = "add", path = "/fields/System.State", value = state });
            }

            var response = await this.InvokeAsync<GetWorkItemResponse>(
                "PATCH",
                null,
                $"wit/workitems/{id}",
                QueryString.Default,
                args.ToArray(),
                contentType: "application/json-patch+json"
            ).ConfigureAwait(false);

            return response;
        }

        public async Task<GetWorkItemResponse[]> GetWorkItemsAsync(string wiql)
        {
            var wiqlResponse = await this.InvokeAsync<GetWiqlResponse>(
                "POST",
                null,
                "wit/wiql",
                new QueryString { ApiVersion = "1.0" },
                new { query = wiql }
            ).ConfigureAwait(false);

            var workItemsResponse = await this.InvokeAsync<GetWorkItemsResponse>(
                "GET",
                null,
                "wit/workitems",
                new QueryString
                {
                    ApiVersion = "1.0",
                    Ids = wiqlResponse.workItems.Select(w => w.id),
                    Expand = "links"
                }
            ).ConfigureAwait(false);

            return workItemsResponse.value;
        }

        public async Task<GetIterationResponse[]> GetIterationsAsync(string project)
        {
            var response = await this.InvokeAsync<GetIterationsResponse>("GET", project, "work/teamsettings/iterations", new QueryString { Timeframe = "current" }).ConfigureAwait(false);
            return response.value;
        }

        public async Task<GetWorkItemTypeResponse[]> GetWorkItemTypesAsync(string project)
        {
            var response = await this.InvokeAsync<GetWorkItemTypesResponse>("GET", project, "wit/workitemtypes", QueryString.Default).ConfigureAwait(false);
            return response.value;
        }

        public async Task<GetTeamProjectResponse[]> GetProjectsAsync()
        {
            var response = await this.InvokeAsync<GetTeamProjectsResponse>("GET", null, "projects", QueryString.Default).ConfigureAwait(false);
            return response.value;
        }

        public async Task<GetBuildResponse> GetBuildAsync(string project, int buildId)
        {
            return await this.InvokeAsync<GetBuildResponse>("GET", project, $"build/builds/{buildId}", QueryString.Default).ConfigureAwait(false);
        }

        public async Task<GetBuildResponse[]> GetBuildsAsync(string project, string buildNumber = null, string resultFilter = null, string statusFilter = null, int? top = null)
        {
            var query = new QueryString() { BuildNumber = buildNumber, ResultFilter = resultFilter, StatusFilter = statusFilter, Top = top };

            var response = await this.InvokeAsync<GetBuildsResponse>("GET", project, "build/builds", query).ConfigureAwait(false);
            return response.value;
        }

        public async Task<GetBuildResponse[]> GetBuildsAsync(string project, int buildDefinition, string buildNumber = null, string resultFilter = null, string statusFilter = null, int? top = null)
        {
            var query = new QueryString() { Definition = buildDefinition, BuildNumber = buildNumber, ResultFilter = resultFilter, StatusFilter = statusFilter, Top = top };

            var response = await this.InvokeAsync<GetBuildsResponse>("GET", project, "build/builds", query).ConfigureAwait(false);
            return response.value;
        }

        public async Task<GetBuildDefinitionResponse> GetBuildDefinitionAsync(string project, string name)
        {
            var response = await this.InvokeAsync<GetBuildDefinitionsResponse>("GET", project, "build/definitions", QueryString.Default).ConfigureAwait(false);
            var definition = response.value.FirstOrDefault(d => string.Equals(d.name, name, StringComparison.OrdinalIgnoreCase));

            return definition;
        }

        public async Task<GetBuildDefinitionResponse[]> GetBuildDefinitionsAsync(string project)
        {
            var response = await this.InvokeAsync<GetBuildDefinitionsResponse>("GET", project, "build/definitions", QueryString.Default).ConfigureAwait(false);
            return response.value;
        }

        public async Task<GetBuildResponse> QueueBuildAsync(string project, int definitionId)
        {
            return await this.InvokeAsync<GetBuildResponse>(
                "POST",
                project,
                "build/builds",
                QueryString.Default,
                new
                {
                    definition = new { id = definitionId }
                }
            ).ConfigureAwait(false);
        }

        public async Task<Artifact[]> GetArtifactsAsync(string project, int buildId)
        {
            var response = await this.InvokeAsync<GetBuildArtifactsResponse>("GET", project, $"build/builds/{buildId}/artifacts", QueryString.Default).ConfigureAwait(false);
            return response.value;
        }

        public async Task<Stream> DownloadArtifactAsync(string project, int buildId, string artifactName)
        {
            var response = await this.InvokeAsync<GetBuildArtifactsResponse>("GET", project, $"build/builds/{buildId}/artifacts", QueryString.Default).ConfigureAwait(false);
            var artifact = response.value.FirstOrDefault(a => string.Equals(a.name, artifactName, StringComparison.OrdinalIgnoreCase));
            if (artifact == null)
                throw new InvalidOperationException($"Artifact \"{artifactName}\" could not be found for build ID # {buildId}.");

            return await this.DownloadStreamAsync(artifact.resource.downloadUrl).ConfigureAwait(false);
        }

        private async Task<Stream> DownloadStreamAsync(string url)
        {
            var request = WebRequest.CreateHttp(url);
            var httpRequest = request as HttpWebRequest;
            if (httpRequest != null)
                httpRequest.UserAgent = "BuildMasterAzureDevOpsExtension/" + typeof(RestApi).Assembly.GetName().Version.ToString();
            request.Method = "GET";

            this.log?.LogDebug($"Downloading Azure DevOps file from URL: {url}");

            this.SetCredentials(request);

            try
            {
                var response = await request.GetResponseAsync().ConfigureAwait(false);
                return response.GetResponseStream();
            }
            catch (WebException ex)
            {
                if (ex.Response == null)
                    throw;

                using var responseStream = ex.Response.GetResponseStream();
                string message = null;
                try
                {
                    message = new StreamReader(responseStream).ReadToEnd();
                }
                catch
                {
                }

                if (message == null)
                    throw;

                throw new Exception(message, ex);
            }
        }

        public async Task<GitRepository[]> GetRepositoriesAsync(string project)
        {
            var query = new QueryString() { ApiVersion = "4.1" };

           var response = await this.InvokeAsync<GitRepositoryResponse>("GET", project, "git/repositories", query).ConfigureAwait(false);
            return response.value;
        }
        public async Task<GitRef[]> GetBranchesAsync(string project, string repository)
        {
            var query = new QueryString() { ApiVersion = "4.1" };

           var response = await this.InvokeAsync<GitRefResponse>("GET", project, $"git/repositories/{repository}/refs", query).ConfigureAwait(false);
            return response.value;
        }

        private async Task<T> InvokeAsync<T>(string method, string project, string relativeUrl, QueryString query, object data = null, string contentType = "application/json")
        {
            string apiBaseUrl;
            if (string.IsNullOrEmpty(project))
                apiBaseUrl = $"{this.instanceUrl}/_apis/";
            else
                apiBaseUrl = $"{this.instanceUrl}/{Uri.EscapeUriString(project)}/_apis/";

            string url = apiBaseUrl + relativeUrl + query.ToString();

            var request = WebRequest.CreateHttp(url);
            var httpRequest = request as HttpWebRequest;
            if (httpRequest != null)
                httpRequest.UserAgent = "BuildMasterAzureDevOpsExtension/" + typeof(RestApi).Assembly.GetName().Version.ToString();
            request.ContentType = contentType;
            request.Method = method;

            this.log?.LogDebug($"Invoking Azure DevOps REST API {method} request ({contentType}) to URL: {url}");

            if (data != null)
            {
                using var writer = new StreamWriter(await request.GetRequestStreamAsync().ConfigureAwait(false), InedoLib.UTF8Encoding);
                JsonSerializer.CreateDefault().Serialize(writer, data);
            }

            this.SetCredentials(request);

            try
            {
                using var response = await request.GetResponseAsync().ConfigureAwait(false);
                return DeserializeJson<T>(response);
            }
            catch (WebException ex) when (ex.Response != null)
            {
                throw AzureDevOpsRestException.Wrap(ex, url);
            }
        }

        internal static T DeserializeJson<T>(WebResponse response)
        {
            using var reader = new StreamReader(response.GetResponseStream());
            using var jsonReader = new JsonTextReader(reader);
            try
            {
                return JsonSerializer.CreateDefault().Deserialize<T>(jsonReader);
            }
            catch (JsonReaderException ex)
            {
                int code = (int?)(response as HttpWebResponse)?.StatusCode ?? 500;
                throw new AzureDevOpsRestException(code, "Unable to parse API response, this is likely due to authentication " +
                    $"redirects. Ensure a valid personal access token is configured in the resource credentials. Status code: {code}", ex);
            }
        }

        private void SetCredentials(HttpWebRequest request)
        {
            if (this.token != null)
            {
                this.log?.LogDebug($"Setting Authorization header to token value...");
                request.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + AH.Unprotect(this.token)));
            }
            else
            {
                this.log?.LogDebug("No personal access token specified, no authorization header will be sent.");
            }
        }
    }

    internal sealed class AzureDevOpsRestException : Exception
    {
        public AzureDevOpsRestException(int statusCode, string message, Exception inner)
            : base(message, inner)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; }

        public string FullMessage => $"The server returned an error ({this.StatusCode}): {this.Message}";

        public static AzureDevOpsRestException Wrap(WebException ex, string url)
        {
            var response = (HttpWebResponse)ex.Response;
            try
            {
                var error = RestApi.DeserializeJson<Error>(response);
                return new AzureDevOpsRestException((int)response.StatusCode, error.message, ex);
            }
            catch
            {
                using (var responseStream = ex.Response.GetResponseStream())
                {
                    try
                    {
                        string errorText = new StreamReader(responseStream).ReadToEnd();
                        return new AzureDevOpsRestException((int)response.StatusCode, errorText, ex);
                    }
                    catch
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                            return new AzureDevOpsRestException((int)response.StatusCode, "Verify that the credentials used to connect are correct.", ex);
                        if (response.StatusCode == HttpStatusCode.NotFound)
                            return new AzureDevOpsRestException(404, $"Verify that the URL in the operation or credentials is correct (resolved to '{url}').", ex);

                        throw ex;
                    }
                }
            }
        }
    }
}
