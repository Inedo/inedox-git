using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Extensions.GitLab.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.Clients
{
    internal sealed class GitLabClient
    {
        public const string GitLabComUrl = "https://gitlab.com/api";

        static GitLabClient()
        {
            // Ensure TLS 1.2 is supported. See https://about.gitlab.com/2018/10/15/gitlab-to-deprecate-older-tls/
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;
        }

        private string apiBaseUrl;

        private static string Esc(string part) => Uri.EscapeDataString(part ?? string.Empty);
        private static string Esc(object part) => Esc(part?.ToString());
        private string EscapeFullProjectPath(string project)
        {
            if (!string.IsNullOrEmpty(this.GroupName))
                return Uri.EscapeDataString(this.GroupName + "/" + project);
            else
                return Uri.EscapeDataString(project ?? string.Empty);
        }

        public GitLabClient(string apiBaseUrl, string userName, SecureString password, string groupName)
        {
            if (!string.IsNullOrEmpty(userName) && password == null)
                throw new InvalidOperationException("If a username is specified, a personal access token must be specified in the operation or in the resource credential.");

            this.apiBaseUrl = AH.CoalesceString(apiBaseUrl, GitLabClient.GitLabComUrl).TrimEnd('/');
            this.UserName = userName;
            this.Password = password;
            this.GroupName = AH.NullIf(groupName, string.Empty);
        }

        
        public GitLabClient(GitLabSecureCredentials credentials, GitLabSecureResource resource)
        {
            this.apiBaseUrl = AH.CoalesceString(resource.ApiUrl, GitLabClient.GitLabComUrl).TrimEnd('/');
            this.UserName = credentials.UserName;
            this.Password = credentials.PersonalAccessToken;
            this.GroupName = AH.NullIf(resource.GroupName, string.Empty);
        }

        public string GroupName { get; }
        public string UserName { get; }
        public SecureString Password { get; }

        public async Task<IList<JObject>> GetGroupsAsync(CancellationToken cancellationToken)
        {
            var results = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/groups?per_page=100", cancellationToken).ConfigureAwait(false);
            return results.Cast<JObject>().ToList();
        }

        public async Task<IList<JObject>> GetProjectsAsync(CancellationToken cancellationToken)
        {
            string uri;
            if (!string.IsNullOrEmpty(this.GroupName))
                uri = $"{this.apiBaseUrl}/v4/groups/{Esc(this.GroupName)}/projects?per_page=100";
            else
                uri = $"{this.apiBaseUrl}/v4/projects?owned=true&per_page=100";

            var results = await this.InvokePagesAsync("GET", uri, cancellationToken).ConfigureAwait(false);
            return results.Cast<JObject>().ToList();
        }

        public async Task<JObject> GetProjectAsync(string projectName, CancellationToken cancellationToken)
        {
            try
            {
                var project = await this.InvokeAsync("GET", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(projectName)}", cancellationToken).ConfigureAwait(false);
                return (JObject)project;
            }
            catch (GitLabRestException ex) when (ex.StatusCode == 404)
            {
                return null;
            }
        }

        public async Task<int?> FindUserAsync(string name, CancellationToken cancellationToken)
        {
            var users = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/users?username={Esc(name)}", cancellationToken).ConfigureAwait(false);
            return (int?)users.Cast<JObject>().FirstOrDefault()?["id"];
        }

        public async Task<IList<JObject>> GetIssuesAsync(string repositoryName, GitLabIssueFilter filter, CancellationToken cancellationToken)
        {
            var issues = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/issues{filter.ToQueryString()}", cancellationToken).ConfigureAwait(false);
            return issues.Cast<JObject>().ToList();
        }

        public async Task<int> CreateIssueAsync(string repositoryName, object data, CancellationToken cancellationToken)
        {
            var issue = (JObject)await this.InvokeAsync("POST", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/issues", data, cancellationToken);
            return (int)issue["iid"];
        }
        public Task UpdateIssueAsync(int issueId, string repositoryName, object update, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("PUT", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/issues/{issueId}", update, cancellationToken);
        }

        public async Task<int> CreateMilestoneAsync(string milestone, string repositoryName, CancellationToken cancellationToken)
        {
            int? milestoneId = await this.FindMilestoneAsync(milestone, repositoryName, cancellationToken).ConfigureAwait(false);
            if (milestoneId.HasValue)
                return milestoneId.Value;

            var data = (JObject)await this.InvokeAsync("POST", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/milestones", new { title = milestone }, cancellationToken).ConfigureAwait(false);
            return (int)data["id"];
        }
        public async Task CloseMilestoneAsync(string milestone, string repositoryName, CancellationToken cancellationToken)
        {
            int? milestoneId = await this.FindMilestoneAsync(milestone, repositoryName, cancellationToken).ConfigureAwait(false);
            if (milestoneId == null)
                return;

            await this.InvokeAsync("PUT", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/milestones/{milestoneId}", new { state_event = "close" }, cancellationToken).ConfigureAwait(false);
        }
        public Task UpdateMilestoneAsync(int milestoneId, string repositoryName, object data, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("PUT", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/milestones/{milestoneId}", data, cancellationToken);
        }

        public Task CreateCommentAsync(int issueId, string repositoryName, string commentText, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("POST", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/issues/{issueId}/notes", new { body = commentText }, cancellationToken);
        }

        public async Task<int?> FindMilestoneAsync(string title, string repositoryName, CancellationToken cancellationToken)
        {
            var milestones = await this.GetMilestonesAsync(repositoryName, null, cancellationToken).ConfigureAwait(false);

            return milestones
                .Where(m => string.Equals(m["title"]?.ToString() ?? string.Empty, title, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Value<int?>("id"))
                .FirstOrDefault();
        }

        public async Task<IList<JObject>> GetMilestonesAsync(string repositoryName, string state, CancellationToken cancellationToken)
        {
            var milestones = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/milestones?per_page=100{(string.IsNullOrEmpty(state) ? string.Empty : "&state=" + Uri.EscapeDataString(state))}", cancellationToken).ConfigureAwait(false);
            if (milestones == null)
                return new JObject[0];

            return milestones.Cast<JObject>().ToList();
        }

        public async Task<JObject> GetTagAsync(string repositoryName, string tag, CancellationToken cancellationToken)
        {
            try
            {
                var tagData = await this.InvokeAsync("GET", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/repository/tags/{Esc(tag)}", cancellationToken).ConfigureAwait(false);
                return (JObject)tagData;
            }
            catch (GitLabRestException ex) when (ex.StatusCode == 404)
            {
                return null;
            }
        }

        public async Task EnsureReleaseAsync(string repositoryName, string tag, string description, CancellationToken cancellationToken)
        {
            var uri = $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/repository/tags/{Esc(tag)}/release";
            var data = new
            {
                tag_name = tag,
                description = description
            };

            try
            {
                await this.InvokeAsync("POST", uri, data, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (((ex.InnerException as WebException)?.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Conflict)
            {
                await this.InvokeAsync("PUT", uri, data, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<IList<string>> GetBranchesAsync(string repositoryName, CancellationToken cancellationToken)
        {
            var branchData = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/projects/{EscapeFullProjectPath(repositoryName)}/repository/branches", cancellationToken).ConfigureAwait(false);
            return branchData
                .Cast<JObject>()
                .Select(b => b["name"].ToString())
                .OrderBy(b => b)
                .ToList();
        }

        private static LazyRegex NextPageLinkPattern = new LazyRegex("<(?<uri>[^>]+)>; rel=\"next\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private async Task<IEnumerable<object>> InvokePagesAsync(string method, string uri, CancellationToken cancellationToken)
        {
            return (IEnumerable<object>)await this.InvokeAsync(method, uri, null, true, cancellationToken).ConfigureAwait(false);
        }
        private Task<object> InvokeAsync(string method, string url, CancellationToken cancellationToken)
        {
            return this.InvokeAsync(method, url, null, false, cancellationToken);
        }
        private Task<object> InvokeAsync(string method, string url, object data, CancellationToken cancellationToken)
        {
            return this.InvokeAsync(method, url, data, false, cancellationToken);
        }
        private async Task<object> InvokeAsync(string method, string uri, object data, bool allPages, CancellationToken cancellationToken)
        {
            var request = WebRequest.CreateHttp(uri);
            request.UserAgent = "InedoGitLabExtension/" + typeof(GitLabClient).Assembly.GetName().Version.ToString();
            request.Method = method;

            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers["PRIVATE-TOKEN"] = AH.Unprotect(this.Password);

            using (cancellationToken.Register(() => request.Abort()))
            {
                if (data != null)
                {
                    using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                    using (var writer = new StreamWriter(requestStream, InedoLib.UTF8Encoding))
                    {
                        JsonSerializer.CreateDefault().Serialize(writer, data);
                    }
                }

                try
                {
                    using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                    using (var responseStream = response.GetResponseStream())
                    using (var reader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        var responseJson = JsonSerializer.CreateDefault().Deserialize(jsonReader);
                        if (allPages)
                        {
                            var nextPage = NextPageLinkPattern.Match(response.Headers["Link"] ?? "");
                            if (nextPage.Success)
                            {
                                responseJson = ((IEnumerable<object>)responseJson).Concat((IEnumerable<object>)await this.InvokeAsync(method, nextPage.Groups["uri"].Value, data, true, cancellationToken).ConfigureAwait(false));
                            }
                        }
                        return responseJson;
                    }
                }
                catch (WebException ex) when (ex.Status == WebExceptionStatus.RequestCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                catch (WebException ex) when (ex.Response != null)
                {
                    throw GitLabRestException.Wrap(ex, uri);
                }
            }
        }
    }

    internal sealed class GitLabIssueFilter
    {
        public string Milestone { get; set; }
        public string Labels { get; set; }
        public string CustomFilterQueryString { get; set; }

        public string ToQueryString()
        {
            if (!string.IsNullOrEmpty(this.CustomFilterQueryString))
                return this.CustomFilterQueryString;

            var buffer = new StringBuilder("?per_page=100", 128);
            if (!string.IsNullOrEmpty(this.Milestone))
                buffer.Append("&milestone=").Append(Uri.EscapeDataString(this.Milestone));
            if (!string.IsNullOrEmpty(this.Labels))
                buffer.Append("&labels=").Append(Uri.EscapeDataString(this.Labels));

            return buffer.ToString();
        }
    }

    internal sealed class GitLabRestException : Exception
    {
        public GitLabRestException(int statusCode, string message, Exception inner)
            : base(message, inner)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; }

        public string FullMessage => $"The server returned an error ({this.StatusCode}): {this.Message}";

        public static GitLabRestException Wrap(WebException ex, string url)
        {
            var response = (HttpWebResponse)ex.Response;

            using (var responseStream = ex.Response.GetResponseStream())
            using (var reader = new StreamReader(responseStream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                try
                {
                    var obj = JObject.ReadFrom(jsonReader);
                    string errorText = obj["message"].ToString();
                    return new GitLabRestException((int)response.StatusCode, errorText, ex);
                }
                catch
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                        return new GitLabRestException((int)response.StatusCode, "Verify that the credentials used to connect are correct.", ex);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return new GitLabRestException(404, $"Verify that the URL in the operation or credentials is correct (resolved to '{url}').", ex);

                    throw ex;
                }
            }            
        }
    }
}
