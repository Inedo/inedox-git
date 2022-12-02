using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.GitLab.IssueSources;

namespace Inedo.Extensions.GitLab.Clients
{
    internal sealed class GitLabClient
    {
        public const string GitLabComUrl = "https://gitlab.com/api";
        public const string PasswordDisplayName = "Personal access token";
        public const string ApiUrlDisplayName = "API URL";
        public const string ApiUrlPlaceholderText = GitLabComUrl;

        private static readonly LazyRegex NextPageLinkPattern = new("<(?<uri>[^>]+)>; rel=\"next\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly string apiBaseUrl;

        public GitLabClient(string apiBaseUrl, string userName, SecureString password)
        {
            if (!string.IsNullOrEmpty(userName) && password == null)
                throw new InvalidOperationException("If a username is specified, a personal access token must be specified in the operation or in the resource credential.");

            this.apiBaseUrl = AH.CoalesceString(apiBaseUrl, GitLabClient.GitLabComUrl).TrimEnd('/');
            this.UserName = userName;
            this.Password = password;
        }
        public GitLabClient(GitLabRepository resource, ICredentialResolutionContext context)
            : this((GitLabAccount)resource.GetCredentials(context))
        {
            if (resource.LegacyApiUrl != null)
                apiBaseUrl = resource.LegacyApiUrl;
        }
        public GitLabClient(GitServiceCredentials credentials)
        {
            this.apiBaseUrl = AH.CoalesceString(credentials?.ServiceUrl, GitLabClient.GitLabComUrl).TrimEnd('/');
            this.UserName = credentials?.UserName;
            this.Password = credentials?.Password;
        }
        public GitLabClient(GitServiceCredentials credentials, GitLabRepository resource)
            : this(credentials)
        {
            if (resource.LegacyApiUrl != null)
                apiBaseUrl = resource.LegacyApiUrl;
        }

        public string UserName { get; }
        public SecureString Password { get; }

        public IAsyncEnumerable<string> GetGroupsAsync(CancellationToken cancellationToken)
        {
            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/v4/groups?per_page=100",
                d => SelectString(d, "full_path"),
                cancellationToken
            );
        }
        public IAsyncEnumerable<string> GetProjectsAsync(string groupName, CancellationToken cancellationToken)
        {
            string uri;
            if (!string.IsNullOrEmpty(groupName))
                uri = $"{this.apiBaseUrl}/v4/groups/{Esc(groupName)}/projects?per_page=100";
            else
                uri = $"{this.apiBaseUrl}/v4/projects?owned=true&per_page=100";

            return this.InvokePagesAsync(uri, d => SelectString(d, "path"), cancellationToken);
        }
        public async Task<GitLabProjectInfo> GetProjectAsync(GitLabProjectId id, CancellationToken cancellationToken)
        {
            using var jdoc = await this.InvokeAsync(HttpMethod.Get, $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}", null, cancellationToken).ConfigureAwait(false);
            var element = jdoc.RootElement;
            return new GitLabProjectInfo
            {
                RepositoryUrl = element.GetProperty("http_url_to_repo").GetString(),
                BrowseUrl = element.GetProperty("web_url").GetString(),
                DefaultBranch = element.GetProperty("default_branch").GetString()
            };
        }
        public async Task<long?> FindUserAsync(string name, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(HttpMethod.Get, $"{this.apiBaseUrl}/v4/users?username={Esc(name)}", null, cancellationToken).ConfigureAwait(false);
            foreach (var obj in doc.RootElement.EnumerateArray())
                return obj.GetProperty("id").GetInt64();

            return null;
        }
        public IAsyncEnumerable<GitLabIssue> GetIssuesAsync(GitLabProjectId id, GitLabIssueFilter filter, CancellationToken cancellationToken)
        {
            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/issues{filter.ToQueryString()}",
                getIssues,
                cancellationToken
            );

            static IEnumerable<GitLabIssue> getIssues(JsonDocument doc)
            {
                foreach (var obj in doc.RootElement.EnumerateArray())
                    yield return new GitLabIssue(obj);
            }
        }
        public async Task<long> CreateIssueAsync(GitLabProjectId id, object data, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/issues",
                data,
                cancellationToken
            ).ConfigureAwait(false);

            return doc.RootElement.GetProperty("iid").GetInt64();
        }
        public async Task UpdateIssueAsync(int issueId, GitLabProjectId id, object update, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Put,
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/issues/{issueId}",
                update,
                cancellationToken
            ).ConfigureAwait(false);
        }
        public async Task<long> CreateMilestoneAsync(string milestone, GitLabProjectId id, CancellationToken cancellationToken)
        {
            long? milestoneId = await this.FindMilestoneAsync(milestone, id, cancellationToken).ConfigureAwait(false);
            if (milestoneId.HasValue)
                return milestoneId.Value;

            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/milestones",
                new { title = milestone },
                cancellationToken
            ).ConfigureAwait(false);

            return doc.RootElement.GetProperty("id").GetInt64();
        }
        public async Task CloseMilestoneAsync(string milestone, GitLabProjectId id, CancellationToken cancellationToken)
        {
            long? milestoneId = await this.FindMilestoneAsync(milestone, id, cancellationToken).ConfigureAwait(false);
            if (milestoneId == null)
                return;

            using var doc = await this.InvokeAsync(
                HttpMethod.Put,
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/milestones/{milestoneId}",
                new { state_event = "close" },
                cancellationToken
            ).ConfigureAwait(false);
        }
        public async Task UpdateMilestoneAsync(long milestoneId, GitLabProjectId id, object data, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Put,
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/milestones/{milestoneId}",
                data,
                cancellationToken
            ).ConfigureAwait(false);
        }
        public async Task CreateCommentAsync(int issueId, GitLabProjectId id, string commentText, CancellationToken cancellationToken)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/issues/{issueId}/notes",
                new { body = commentText },
                cancellationToken
            ).ConfigureAwait(false);
        }
        public async Task<long?> FindMilestoneAsync(string title, GitLabProjectId id, CancellationToken cancellationToken)
        {
            await foreach (var m in this.GetMilestonesAsync(id, null, cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(m.Title, title, StringComparison.OrdinalIgnoreCase))
                    return m.Id;
            }

            return null;
        }
        public IAsyncEnumerable<GitLabMilestone> GetMilestonesAsync(GitLabProjectId id, string state, CancellationToken cancellationToken)
        {
            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/milestones?per_page=100{(string.IsNullOrEmpty(state) ? string.Empty : "&state=" + Uri.EscapeDataString(state))}",
                getMilestones,
                cancellationToken
            );

            static IEnumerable<GitLabMilestone> getMilestones(JsonDocument doc)
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var obj in doc.RootElement.EnumerateArray())
                    {
                        if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        {
                            if (obj.TryGetProperty("iid", out var iid) && iid.ValueKind == JsonValueKind.Number)
                            {
                                yield return new GitLabMilestone(
                                    iid.GetInt64(),
                                    title.GetString(),
                                    GetStringOrDefault(obj, "description"),
                                    GetStringOrDefault(obj, "start_date"),
                                    GetStringOrDefault(obj, "due_date"),
                                    GetStringOrDefault(obj, "state")
                                );
                            }
                        }
                    }
                }
            }
        }
        public async Task<GitLabTag> GetTagAsync(GitLabProjectId id, string tag, CancellationToken cancellationToken)
        {
            try
            {
                using var doc = await this.InvokeAsync(
                    HttpMethod.Get,
                    $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/repository/tags/{Esc(tag)}",
                    null,
                    cancellationToken
                ).ConfigureAwait(false);

                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("release", out var release) && release.ValueKind == JsonValueKind.Object)
                    return new GitLabTag(release.GetProperty("tag_name").GetString(), GetStringOrDefault(release, "description"));
            }
            catch (GitLabRestException ex) when (ex.StatusCode == 404)
            {
            }

            return null;
        }
        public async Task EnsureReleaseAsync(GitLabProjectId id, string tag, string description, CancellationToken cancellationToken)
        {
            var uri = $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/repository/tags/{Esc(tag)}/release";
            var data = new
            {
                tag_name = tag,
                description
            };

            try
            {
                using var doc = await this.InvokeAsync(HttpMethod.Post, uri, data, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (((ex.InnerException as WebException)?.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Conflict)
            {
                using var doc = await this.InvokeAsync(HttpMethod.Put, uri, data, cancellationToken).ConfigureAwait(false);
            }
        }
        public IAsyncEnumerable<GitRemoteBranch> GetBranchesAsync(GitLabProjectId id, CancellationToken cancellationToken)
        {
            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/repository/branches",
                selectBranches,
                cancellationToken
            );

            static IEnumerable<GitRemoteBranch> selectBranches(JsonDocument d)
            {
                foreach (var e in d.RootElement.EnumerateArray())
                {
                    if (!e.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
                        continue;

                    var name = nameProp.GetString();

                    if (!e.TryGetProperty("commit", out var commit) || commit.ValueKind != JsonValueKind.Object || !commit.TryGetProperty("id", out var sha) || sha.ValueKind != JsonValueKind.String)
                        continue;

                    if (!GitObjectId.TryParse(sha.GetString(), out var hash))
                        continue;

                    bool isProtected = e.TryGetProperty("protected", out var p) && p.ValueKind == JsonValueKind.True;

                    yield return new GitRemoteBranch(hash, name, isProtected);
                }
            }
        }
        public IAsyncEnumerable<GitPullRequest> GetPullRequestsAsync(GitLabProjectId id, bool includeClosed, CancellationToken cancellationToken = default)
        {
            return this.InvokePagesAsync(
                $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/merge_requests?state={(includeClosed ? "all" : "opened")}",
                selectPullRequests,
                cancellationToken
            );

            static IEnumerable<GitPullRequest> selectPullRequests(JsonDocument d)
            {
                foreach (var pr in d.RootElement.EnumerateArray())
                {
                    var url = pr.GetProperty("web_url").GetString();
                    var id = pr.GetProperty("id").ToString();
                    bool closed = !pr.GetProperty("state").ValueEquals("opened");
                    var title = pr.GetProperty("title").GetString();
                    var from = pr.GetProperty("source_branch").GetString();
                    var to = pr.GetProperty("target_branch").GetString();

                    yield return new GitPullRequest(id, url, title, closed, from, to);
                }
            }
        }
        public async Task SetCommitStatusAsync(GitLabProjectId id, string commit, string status, string description, string context, CancellationToken cancellationToken)
        {
            var url = $"{this.apiBaseUrl}/v4/projects/{id.ToUriFragment()}/statuses/{Uri.EscapeDataString(commit)}?state={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrEmpty(description))
                url += $"&description={Uri.EscapeDataString(description)}";
            if (!string.IsNullOrEmpty(context))
                url += $"&context={Uri.EscapeDataString(context)}";

            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                url,
                null,
                cancellationToken
            ).ConfigureAwait(false);
        }
        public async Task MergeMergeRequestAsync(GitLabProjectId projectId, int requestId, string message, string sha, bool squash, CancellationToken cancellationToken = default)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Put,
                $"{apiBaseUrl}/v4/projects/{projectId.ToUriFragment()}/merge_requests/{requestId}/merge",
                new
                {
                    squash,
                    merge_commit_message = message,
                    squash_commit_message = message,
                    sha
                },
                cancellationToken
            ).ConfigureAwait(false);
        }
        public async Task<int> CreateMergeRequestAsync(GitLabProjectId projectId, string sourceBranch, string targetBranch, string title, string description, CancellationToken cancellationToken = default)
        {
            using var doc = await this.InvokeAsync(
                HttpMethod.Post,
                $"{apiBaseUrl}/v4/projects/{projectId.ToUriFragment()}/merge_requests",
                new
                {
                    source_branch = sourceBranch,
                    target_branch = targetBranch,
                    title,
                    description
                },
                cancellationToken
            ).ConfigureAwait(false);

            return doc.RootElement.GetProperty("iid").GetInt32();
        }

        private static string Esc(string part) => Uri.EscapeDataString(part ?? string.Empty);

        private async Task<JsonDocument> InvokeAsync(HttpMethod method, string url, object data, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers.Add("PRIVATE-TOKEN", AH.Unprotect(this.Password));

            if (data != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(data, data.GetType());
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Content = content;
            }

            using var response = await SDK.CreateHttpClient().SendAsync(request, cancellationToken).ConfigureAwait(false);
            await GitLabRestException.ThrowIfErrorAsync(response, url, cancellationToken).ConfigureAwait(false);
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
                if (!string.IsNullOrEmpty(this.UserName))
                    request.Headers.Add("PRIVATE-TOKEN", AH.Unprotect(this.Password));

                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                await GitLabRestException.ThrowIfErrorAsync(response, url, cancellationToken).ConfigureAwait(false);
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
