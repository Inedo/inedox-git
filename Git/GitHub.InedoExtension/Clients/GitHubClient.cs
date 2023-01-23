using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility.Git;
using Inedo.Extensions.GitHub.IssueSources;

namespace Inedo.Extensions.GitHub.Clients
{
    internal sealed class GitHubClient
    {
        public const string GitHubComUrl = "https://api.github.com";
        private static readonly LazyRegex NextPageLinkPattern = new("<(?<uri>[^>]+)>; rel=\"next\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly string[] EnabledPreviews = new[]
        {
            "application/vnd.github.inertia-preview+json", // projects
        };
        private readonly string apiBaseUrl;
        private readonly ILogSink log;

        public GitHubClient(string apiBaseUrl, string userName, SecureString password, ILogSink log = null)
        {
            if (!string.IsNullOrEmpty(userName) && password == null)
                throw new InvalidOperationException("If a username is specified, a password must be specified in the operation or in the resource credential.");

            this.apiBaseUrl = AH.CoalesceString(apiBaseUrl, GitHubClient.GitHubComUrl).TrimEnd('/');
            this.UserName = userName;
            this.Password = password;
            this.log = log;
        }
        public GitHubClient(GitHubAccount credentials, GitHubRepository resource, ILogSink log = null)
        {
            this.apiBaseUrl = AH.CoalesceString(resource?.LegacyApiUrl, GitHubComUrl).TrimEnd('/');
            this.UserName = credentials?.UserName;
            this.Password = credentials?.Password;
            this.log = log;
        }

        public string UserName { get; }
        public SecureString Password { get; }

        public IAsyncEnumerable<string> GetOrganizationsAsync(CancellationToken cancellationToken)
        {
            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/user/orgs?per_page=100",
                d => SelectString(d, "login"),
                cancellationToken
            );
        }
        public IAsyncEnumerable<string> GetRepositoriesAsync(string organizationName, CancellationToken cancellationToken)
        {
            string url;
            if (!string.IsNullOrEmpty(organizationName))
                url = $"{this.apiBaseUrl}/orgs/{Esc(organizationName)}/repos?per_page=100";
            else
                url = $"{this.apiBaseUrl}/user/repos?per_page=100";

            return this.InvokePagesAsync(
                url,
                d => SelectString(d, "name"),
                cancellationToken
            );
        }
        public async Task<GitHubRepositoryInfo> GetRepositoryAsync(string organizationName, string repositoryName, CancellationToken cancellationToken = default)
        {
            string url;
            if (!string.IsNullOrEmpty(organizationName))
                url = $"{this.apiBaseUrl}/repos/{Esc(organizationName)}/{Esc(repositoryName)}";
            else
                url = $"{this.apiBaseUrl}/repos/{Esc(this.UserName)}/{Esc(repositoryName)}";

            using var doc = await this.InvokeAsync(HttpMethod.Get, url, cancellationToken: cancellationToken).ConfigureAwait(false);
            var obj = doc.RootElement;

            return new GitHubRepositoryInfo
            {
                RepositoryUrl = obj.GetProperty("clone_url").GetString(),
                BrowseUrl = obj.GetProperty("html_url").GetString(),
                DefaultBranch = obj.GetProperty("default_branch").GetString()
            };
        }
        public IAsyncEnumerable<GitRemoteBranch> GetBranchesAsync(string organizationName, string repositoryName, CancellationToken cancellationToken = default)
        {
            string url;
            if (!string.IsNullOrEmpty(organizationName))
                url = $"{this.apiBaseUrl}/repos/{Esc(organizationName)}/{Esc(repositoryName)}/branches?per_page=100";
            else
                url = $"{this.apiBaseUrl}/repos/{Esc(this.UserName)}/{Esc(repositoryName)}/branches?per_page=100";

            return this.InvokePagesAsync(url, selectBranches, cancellationToken);

            static IEnumerable<GitRemoteBranch> selectBranches(JsonDocument d)
            {
                foreach (var e in d.RootElement.EnumerateArray())
                {
                    if (!e.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
                        continue;

                    var name = nameProp.GetString();

                    if (!e.TryGetProperty("commit", out var commit) || commit.ValueKind != JsonValueKind.Object || !commit.TryGetProperty("sha", out var sha) || sha.ValueKind != JsonValueKind.String)
                        continue;

                    if (!GitObjectId.TryParse(sha.GetString(), out var hash))
                        continue;

                    bool isProtected = e.TryGetProperty("protected", out var p) && p.ValueKind == JsonValueKind.True;

                    yield return new GitRemoteBranch(hash, name, isProtected);
                }
            }
        }
        public IAsyncEnumerable<GitPullRequest> GetPullRequestsAsync(string organizationName, string repositoryName, bool includeClosed, CancellationToken cancellationToken = default)
        {
            string url;
            if (!string.IsNullOrEmpty(organizationName))
                url = $"{this.apiBaseUrl}/repos/{Esc(organizationName)}/{Esc(repositoryName)}/pulls?per_page=100";
            else
                url = $"{this.apiBaseUrl}/repos/{Esc(this.UserName)}/{Esc(repositoryName)}/pulls?per_page=100";

            url = $"{url}&state={(includeClosed ? "all" : "open")}";

            return this.InvokePagesAsync(url, selectPullRequests, cancellationToken);

            static IEnumerable<GitPullRequest> selectPullRequests(JsonDocument d)
            {
                foreach (var pr in d.RootElement.EnumerateArray())
                {
                    // skip requests from other repositories for now
                    long sourceRepoId = pr.GetProperty("head").GetProperty("repo").GetProperty("id").GetInt64();
                    long targetRepoId = pr.GetProperty("base").GetProperty("repo").GetProperty("id").GetInt64();
                    if (sourceRepoId != targetRepoId)
                        continue;

                    var url = pr.GetProperty("url").GetString();
                    var id = pr.GetProperty("id").ToString();
                    bool closed = pr.GetProperty("state").ValueEquals("closed");
                    var title = pr.GetProperty("title").GetString();
                    var from = pr.GetProperty("head").GetProperty("ref").GetString();
                    var to = pr.GetProperty("base").GetProperty("ref").GetString();

                    yield return new GitPullRequest(id, url, title, closed, from, to);
                }
            }
        }
        public async Task MergePullRequestAsync(string organizationName, string repositoryName, int id, string headCommit, string message, string method, CancellationToken cancellationToken = default)
        {
            string url;
            if (!string.IsNullOrEmpty(organizationName))
                url = $"{this.apiBaseUrl}/repos/{Esc(organizationName)}/{Esc(repositoryName)}/pulls/";
            else
                url = $"{this.apiBaseUrl}/repos/{Esc(this.UserName)}/{Esc(repositoryName)}/pulls/";

            url += $"{id}/merge";

            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                url,
                new
                {
                    commit_title = message,
                    merge_method = method,
                    sha = headCommit
                },
                cancellationToken: cancellationToken
            );
        }
        public async Task<int> CreatePullRequestAsync(string organizationName, string repositoryName, string source, string target, string title, string description, CancellationToken cancellationToken = default)
        {
            string url;
            if (!string.IsNullOrEmpty(organizationName))
                url = $"{this.apiBaseUrl}/repos/{Esc(organizationName)}/{Esc(repositoryName)}/pulls";
            else
                url = $"{this.apiBaseUrl}/repos/{Esc(this.UserName)}/{Esc(repositoryName)}/pulls";

            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                url,
                new
                {
                    title,
                    body = description,
                    head = source,
                    @base = target
                },
                cancellationToken: cancellationToken
            );

            return doc.RootElement.GetProperty("id").GetInt32();
        }

        public async Task SetCommitStatusAsync(string organizationName, string repositoryName, string commit, string status, string description, string context, CancellationToken cancellationToken)
        {
            string url;
            if (!string.IsNullOrEmpty(organizationName))
                url = $"{this.apiBaseUrl}/repos/{Esc(organizationName)}/{Esc(repositoryName)}/statuses/{Uri.EscapeDataString(commit)}";
            else
                url = $"{this.apiBaseUrl}/repos/{Esc(this.UserName)}/{Esc(repositoryName)}/statuses/{Uri.EscapeDataString(commit)}";

            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                url,
                new
                {
                    state = status,
                    description,
                    context
                },
                cancellationToken: cancellationToken
            );
        }

        public IAsyncEnumerable<GitHubIssue> GetIssuesAsync(string ownerName, string repositoryName, GitHubIssueFilter filter, CancellationToken cancellationToken)
        {
            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/issues{filter.ToQueryString()}",
                getIssues,
                cancellationToken
            );

            static IEnumerable<GitHubIssue> getIssues(JsonDocument doc)
            {
                foreach (var obj in doc.RootElement.EnumerateArray())
                    yield return new GitHubIssue(obj);
            }
        }
        public async Task<GitHubIssue> GetIssueAsync(string issueUrl, string statusOverride = null, bool? closedOverride = null, CancellationToken cancellationToken = default)
        {
            using var doc = await this.InvokeAsync(HttpMethod.Get, issueUrl, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new GitHubIssue(doc.RootElement, statusOverride, closedOverride);
        }
        public async Task<int> CreateIssueAsync(string ownerName, string repositoryName, object data, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/issues",
                data,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return doc.RootElement.GetProperty("number").GetInt32();
        }
        public async Task UpdateIssueAsync(int issueId, string ownerName, string repositoryName, object update, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Patch,
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/issues/{issueId}",
                update,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        public async Task<int> CreateMilestoneAsync(string milestone, string ownerName, string repositoryName, CancellationToken cancellationToken)
        {
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, ownerName, repositoryName, cancellationToken).ConfigureAwait(false);
            if (milestoneNumber.HasValue)
                return milestoneNumber.Value;

            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/milestones",
                new { title = milestone },
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return doc.RootElement.GetProperty("number").GetInt32();
        }

        public async Task UpdateMilestoneAsync(int milestoneNumber, string ownerName, string repositoryName, object data, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Patch,
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/milestones/{milestoneNumber}",
                data,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        public async Task CreateStatusAsync(string ownerName, string repositoryName, string commitHash, string state, string target_url, string description, string context, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/statuses/{Esc(commitHash)}",
                new { state, target_url, description, context },
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        public async Task CreateCommentAsync(int issueId, string ownerName, string repositoryName, string commentText, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/issues/{issueId}/comments",
                new { body = commentText },
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        public async Task<int?> FindMilestoneAsync(string title, string ownerName, string repositoryName, CancellationToken cancellationToken)
        {
            await foreach (var m in this.GetMilestonesAsync(ownerName, repositoryName, "all", cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(m.Title, title, StringComparison.OrdinalIgnoreCase))
                    return m.Number;
            }

            return null;
        }

        public IAsyncEnumerable<GitHubMilestone> GetMilestonesAsync(string ownerName, string repositoryName, string state, CancellationToken cancellationToken)
        {
            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/milestones?state={Uri.EscapeDataString(state)}&sort=due_on&direction=desc&per_page=100",
                d => System.Text.Json.JsonSerializer.Deserialize<IEnumerable<GitHubMilestone>>(d),
                cancellationToken
            );
        }

        public IAsyncEnumerable<GitHubProject> GetProjectsAsync(string ownerName, string repositoryName, CancellationToken cancellationToken)
        {
            var url = $"{this.apiBaseUrl}/orgs/{Esc(ownerName)}/projects?state=all";
            if (!string.IsNullOrEmpty(repositoryName))
                url = $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/projects?state=all";

            return this.InvokePagesAsync(
                url,
                d => System.Text.Json.JsonSerializer.Deserialize<IEnumerable<GitHubProject>>(d),
                cancellationToken
            );
        }

        public async IAsyncEnumerable<ProjectColumnData> GetProjectColumnsAsync(string projectColumnsUrl, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var (cardsUrl, name) in this.InvokePagesAsync(projectColumnsUrl, getColumns, cancellationToken).ConfigureAwait(false))
            {
                var issueUrls = new List<string>();

                await foreach (var issueUrl in this.InvokePagesAsync(cardsUrl, getIssueUrls, cancellationToken).ConfigureAwait(false))
                    issueUrls.Add(issueUrl);

                yield return new ProjectColumnData(name, issueUrls);
            }

            static IEnumerable<(string cardsUrl, string name)> getColumns(JsonDocument doc)
            {
                foreach (var obj in doc.RootElement.EnumerateArray())
                    yield return (obj.GetProperty("cards_url").GetString(), obj.GetProperty("name").GetString());
            }

            static IEnumerable<string> getIssueUrls(JsonDocument doc)
            {
                foreach (var obj in doc.RootElement.EnumerateArray())
                {
                    if (obj.TryGetProperty("content_url", out var value))
                        yield return value.GetString();
                }
            }
        }

        public async Task<GitHubRelease> GetReleaseAsync(string ownerName, string repositoryName, string tag, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Get,
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/releases/tags/{Esc(tag)}",
                nullOn404: true,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return doc != null ? JsonSerializer.Deserialize<GitHubRelease>(doc) : null;
        }

        public IAsyncEnumerable<string> ListRefsAsync(string ownerName, string repositoryName, RefType? type, CancellationToken cancellationToken = default)
        {
            var prefix = type switch
            {
                RefType.Branch => "refs/heads",
                RefType.Tag => "refs/tags",
                _ => "refs"
            };

            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/git/{prefix}",
                getRefs,
                cancellationToken
            );

            IEnumerable<string> getRefs(JsonDocument doc)
            {
                foreach (var obj in doc.RootElement.EnumerateArray())
                {
                    var s = obj.GetProperty("ref").GetString();

                    if (s.StartsWith(prefix))
                        s = s[prefix.Length..];

                    if (s.StartsWith("/"))
                        s = s[1..];

                    yield return s;
                }
            }
        }

        public async Task<GitHubRelease> EnsureReleaseAsync(string ownerName, string repositoryName, string tag, string target, string name, string body, bool? draft, bool? prerelease, CancellationToken cancellationToken)
        {
            var release = new GitHubRelease
            {
                Tag = tag,
                Target = target,
                Title = name,
                Description = body,
                Draft = draft,
                Prerelease = prerelease
            };

            var existingRelease = await this.GetReleaseAsync(ownerName, repositoryName, tag, cancellationToken).ConfigureAwait(false);

            using var doc = existingRelease != null
                ? await this.InvokeAsync(HttpMethod.Patch, $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/releases/{existingRelease.Id}", release, cancellationToken: cancellationToken).ConfigureAwait(false)
                : await this.InvokeAsync(HttpMethod.Post, $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/releases", release, cancellationToken: cancellationToken).ConfigureAwait(false);

            return JsonSerializer.Deserialize<GitHubRelease>(doc);
        }

        public async Task UploadReleaseAssetAsync(string ownerName, string repositoryName, string tag, string name, string contentType, Stream contents, Action<long> reportProgress, CancellationToken cancellationToken)
        {
            var release = await this.GetReleaseAsync(ownerName, repositoryName, tag, cancellationToken).ConfigureAwait(false);
            if (release == null)
                throw new ExecutionFailureException($"No release found with tag {tag} in repository {ownerName}/{repositoryName}");

            var uploadUrl = FormatTemplateUri(release.UploadUrl, name);

            using var content = new StreamContent(contents);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var response = await SDK.CreateHttpClient().PostAsync(uploadUrl, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw await GetErrorResponseExceptionAsync(response).ConfigureAwait(false);
        }

        private static string FormatTemplateUri(string templateUri, string name)
        {
            // quick hack for URI templates since former NuGet package doesn't support target framework v4.5.2 
            // The format of templatedUploadUri is: https://host/repos/org/repoName/releases/1000/assets{?name,label}

            int index = templateUri.IndexOf('{');
            return templateUri[..index] + "?name=" + Uri.EscapeDataString(name);
        }
        private static string Esc(string part) => Uri.EscapeUriString(part ?? string.Empty);
        private static async Task<Exception> GetErrorResponseExceptionAsync(HttpResponseMessage response)
        {
            var errorMessage = $"Server replied with {(int)response.StatusCode}";

            if (response.Content.Headers.ContentType?.MediaType?.StartsWith("application/json") == true)
            {
                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

                string parsedMessage = null;

                if (doc.RootElement.TryGetProperty("message", out var message))
                    parsedMessage = message.GetString();

                if (!string.IsNullOrWhiteSpace(parsedMessage))
                    errorMessage += ": " + parsedMessage;

                if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    var moreDetails = new List<string>();

                    foreach (var d in errors.EnumerateArray())
                        moreDetails.Add($"{GetStringOrDefault(d, "resource")} {GetStringOrDefault(d, "code")}".Trim());

                    if(moreDetails.Count > 0)
                        errorMessage += $" ({string.Join(", ", moreDetails)})";
                }
            }
            else
            {
                var details = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(details))
                    errorMessage += ": " + details;
            }

            return new ExecutionFailureException(errorMessage);
        }
        private async Task<JsonDocument> InvokeAsync(HttpMethod method, string url, object data = null, bool nullOn404 = false, CancellationToken cancellationToken = default)
        {
            this.log?.LogDebug($"{method} {url}");
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Accept.ParseAdd("application/vnd.github.v3+json");
            foreach (var preview in EnabledPreviews)
                request.Headers.Accept.ParseAdd(preview);

            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers.Authorization = new AuthenticationHeaderValue("token", AH.Unprotect(this.Password));

            if (data != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(data, data.GetType(), new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("content/json");
                request.Content = content;
            }

            using var response = await SDK.CreateHttpClient().SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (nullOn404 && response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                throw await GetErrorResponseExceptionAsync(response).ConfigureAwait(false);
            }

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        private async IAsyncEnumerable<T> InvokePagesAsync<T>(string url, Func<JsonDocument, IEnumerable<T>> getItems, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var currentUrl = url;
            var client = SDK.CreateHttpClient();

            while (currentUrl != null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                request.Headers.Accept.ParseAdd("application/vnd.github.v3+json");
                foreach (var preview in EnabledPreviews)
                    request.Headers.Accept.ParseAdd(preview);

                if (!string.IsNullOrEmpty(this.UserName))
                    request.Headers.Authorization = new AuthenticationHeaderValue("token", AH.Unprotect(this.Password));

                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw await GetErrorResponseExceptionAsync(response).ConfigureAwait(false);

                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                foreach (var item in getItems(doc))
                    yield return item;

                currentUrl = null;
                if (response.Headers.TryGetValues("Link", out var links))
                {
                    foreach (var link in links)
                    {
                        var m = NextPageLinkPattern.Match(link);
                        if (m.Success)
                        {
                            currentUrl = m.Groups["uri"].Value;
                            break;
                        }
                    }
                }
            }
        }
        private static IEnumerable<string> SelectString(JsonDocument doc, string name)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var obj in doc.RootElement.EnumerateArray())
                {
                    if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var path) && path.ValueKind == JsonValueKind.String)
                        yield return path.GetString();
                }
            }
        }
        private static string GetStringOrDefault(in JsonElement obj, string propertyName)
        {
            if (obj.TryGetProperty(propertyName, out var value))
                return value.GetString();
            else
                return null;
        }
    }
}
