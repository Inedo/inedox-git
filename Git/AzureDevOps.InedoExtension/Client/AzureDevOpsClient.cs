using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.Git;

#nullable enable

namespace Inedo.Extensions.AzureDevOps.Client
{
    internal sealed class AzureDevOpsClient
    {
        private const string ApiVersion = "4.1";
        private readonly HttpClient http;

        public AzureDevOpsClient(GitServiceCredentials credentials) : this($"{credentials.ServiceUrl!.TrimEnd('/')}/", credentials.Password!)
        {
        }
        public AzureDevOpsClient(string url, SecureString token) : this(url, AH.Unprotect(token)!)
        {
        }
        public AzureDevOpsClient(string url, string token)
        {
            this.http = SDK.CreateHttpClient();
            this.http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes($":{token}")));
            this.http.BaseAddress = new Uri(url);
        }

        public IAsyncEnumerable<AdoProject> GetProjectsAsync(CancellationToken cancellationToken = default)
        {
            var url = $"_apis/projects?api-version={ApiVersion}";
            return this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoProject, cancellationToken);
        }
        public IAsyncEnumerable<AdoRepo> GetRepositoriesAsync(string project, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/git/repositories?api-version={ApiVersion}";
            return this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoRepo, cancellationToken);
        }
        public Task<AdoRepo> GetRepositoryAsync(string project, string repository, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repository)}?api-version={ApiVersion}";
            return this.GetAsync(url, AzureDevOpsJsonContext.Default.AdoRepo, cancellationToken);
        }
        public async IAsyncEnumerable<AdoGitRef> GetBranchesAsync(string project, string repository, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repository)}/refs?api-version={ApiVersion}";
            await foreach (var r in this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoGitRef, cancellationToken).ConfigureAwait(false))
            {
                if (r.Name != null && r.Name.StartsWith("refs/heads/"))
                    yield return r;
            }
        }
        public IAsyncEnumerable<AdoPullRequest> GetPullRequestsAsync(string project, string repository, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repository)}/pullRequests?api-version={ApiVersion}";
            return this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoPullRequest, cancellationToken);
        }
        public IAsyncEnumerable<AdoBuild> GetBuildsAsync(string project, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/build/builds?api-version={ApiVersion}";
            return this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoBuild, cancellationToken);
        }
        public Task<AdoBuild> GetBuildAsync(string project, int buildId, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/build/builds{buildId}?api-version={ApiVersion}";
            return this.GetAsync(url, AzureDevOpsJsonContext.Default.AdoBuild, cancellationToken);
        }
        public IAsyncEnumerable<AdoBuildDef> GetBuildDefinitionsAsync(string project, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/build/definitions?api-version={ApiVersion}";
            return this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoBuildDef, cancellationToken);
        }
        public IAsyncEnumerable<AdoArtifact> GetBuildArtifactsAsync(string project, int buildId, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/build/builds/{buildId}/artifacts?api-version={ApiVersion}";
            return this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoArtifact, cancellationToken);
        }
        public Task<Stream> DownloadBuildArtifactAsync(string downloadUrl, CancellationToken cancellationToken = default)
        {
            return this.http.GetStreamAsync(downloadUrl, cancellationToken);
        }
        public async Task<AdoBuild> QueueBuildAsync(string project, int definitionId, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/build/builds?api-version={ApiVersion}";

            var data = new AdoBuild
            {
                Definition = new AdoBuildDef
                {
                    Id = definitionId
                }
            };

            using var response = await this.http.PostAsJsonAsync(url, data, AzureDevOpsJsonContext.Default.AdoBuild, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return (await JsonSerializer.DeserializeAsync(stream, AzureDevOpsJsonContext.Default.AdoBuild, cancellationToken).ConfigureAwait(false))!;
        }
        public IAsyncEnumerable<AdoIteration> GetIterationsAsync(string project, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/work/teamsettings/iterations?api-version={ApiVersion}";
            return this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoIteration, cancellationToken);
        }
        public async IAsyncEnumerable<AdoWorkItem> GetWorkItemsAsync(string wiql, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var url = $"_apis/wit/wiql?api-version={ApiVersion}";

            using var response = await this.http.PostAsJsonAsync(url, new AdoQuery { Query = wiql }, AzureDevOpsJsonContext.Default.AdoQuery, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var results = (await JsonSerializer.DeserializeAsync(stream, AzureDevOpsJsonContext.Default.AdoWorkItemQueryResult, cancellationToken).ConfigureAwait(false))!;

            var ids = results.WorkItems!.Select(i => i.Id);
            var fields = results.Columns!.Select(c => Uri.EscapeDataString(c.ReferenceName!));

            url = $"_apis/wit/workitems?ids={string.Join(',', ids)}&fields={string.Join(',', fields)}&$expand=links&api-version={ApiVersion}";

            await foreach (var i in this.GetListAsync(url, AzureDevOpsJsonContext.Default.AdoListAdoWorkItem, cancellationToken).ConfigureAwait(false))
                yield return i;
        }
        public async Task<AdoWorkItem> CreateWorkItemAsync(string project, string workItemType, string title, string? description, string? iterationPath, CancellationToken cancellationToken = default)
        {
            var url = $"{Uri.EscapeDataString(project)}/_apis/wit/workitems/${Uri.EscapeDataString(workItemType)}?api-version={ApiVersion}";

            var args = new List<AdoCreateWorkItem>
            {
                new AdoCreateWorkItem
                {
                   Operation = "add",
                   Path = "/fields/System.Title",
                   Value = JsonValue.Create(title)
                }
            };

            if (!string.IsNullOrEmpty(description))
            {
                args.Add(
                    new AdoCreateWorkItem
                    {
                        Operation = "add",
                        Path = "/fields/System.Description",
                        Value = JsonValue.Create(description)
                    }
                );
            }

            if (!string.IsNullOrEmpty(iterationPath))
            {
                args.Add(
                    new AdoCreateWorkItem
                    {
                        Operation = "add",
                        Path = "/fields/System.IterationPath",
                        Value = JsonValue.Create(iterationPath)
                    }
                );
            }

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(args, AzureDevOpsJsonContext.Default.IEnumerableAdoCreateWorkItem), InedoLib.UTF8Encoding, "application/json-patch+json")
            };

            using var response = await this.http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return (await JsonSerializer.DeserializeAsync(responseStream, AzureDevOpsJsonContext.Default.AdoWorkItem, cancellationToken).ConfigureAwait(false))!;
        }
        public async Task<AdoWorkItem> UpdateWorkItemAsync(string id, string? title, string? description, string? iterationPath, string? state, IDictionary<string, RuntimeValue>? otherFields, CancellationToken cancellationToken = default)
        {
            var url = $"_apis/wit/workitems/{Uri.EscapeDataString(id)}?api-version={ApiVersion}";
            var args = new List<AdoCreateWorkItem>();
            if (!string.IsNullOrWhiteSpace(title))
            {
                args.Add(
                    new AdoCreateWorkItem
                    {
                        Operation = "add",
                        Path = "/fields/System.Title",
                        Value = JsonValue.Create(title)
                    }
                );            
            }

            if (!string.IsNullOrEmpty(description))
            {
                args.Add(
                    new AdoCreateWorkItem
                    {
                        Operation = "add",
                        Path = "/fields/System.Description",
                        Value = JsonValue.Create(description)
                    }
                );
            }

            if (!string.IsNullOrEmpty(iterationPath))
            {
                args.Add(
                    new AdoCreateWorkItem
                    {
                        Operation = "add",
                        Path = "/fields/System.IterationPath",
                        Value = JsonValue.Create(iterationPath)
                    }
                );
            }

            if (!string.IsNullOrEmpty(state))
            {
                args.Add(
                    new AdoCreateWorkItem
                    {
                        Operation = "add",
                        Path = "/fields/System.State",
                        Value = JsonValue.Create(state)
                    }
                );
            }

            if (otherFields != null)
            {
                foreach (var f in otherFields)
                {
                    args.Add(
                        new AdoCreateWorkItem
                        {
                            Operation = "add",
                            Path = $"/fields/{f.Key}",
                            Value = JsonValue.Create(f.Value.AsString())
                        }
                    );
                }
            }

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(args, AzureDevOpsJsonContext.Default.IEnumerableAdoCreateWorkItem), InedoLib.UTF8Encoding, "application/json-patch+json")
            };

            using var response = await this.http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return (await JsonSerializer.DeserializeAsync(responseStream, AzureDevOpsJsonContext.Default.AdoWorkItem, cancellationToken).ConfigureAwait(false))!;
        }

        private async IAsyncEnumerable<TItem> GetListAsync<TItem>(string url, JsonTypeInfo<AdoList<TItem>> jsonTypeInfo, [EnumeratorCancellation] CancellationToken cancellationToken)
            where TItem : class, new()
        {
            var list = await this.GetAsync(url, jsonTypeInfo, cancellationToken).ConfigureAwait(false);
            if (list.Value != null)
            {
                foreach (var i in list.Value)
                    yield return i;
            }
        }
        private async Task<TResult> GetAsync<TResult>(string url, JsonTypeInfo<TResult> jsonTypeInfo, CancellationToken cancellationToken)
        {
            using var response = await this.http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return (await JsonSerializer.DeserializeAsync(responseStream, jsonTypeInfo, cancellationToken).ConfigureAwait(false))!;
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.Content.Headers.ContentType?.MediaType == "application/json")
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (doc.RootElement.TryGetProperty("message", out var messageElement))
                        throw new InvalidOperationException(messageElement.GetString());
                }

                throw new InvalidOperationException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
            }
        }
    }
}
