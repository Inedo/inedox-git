using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Extensions.GitHub.Clients
{
    internal sealed class GitHubClient
    {
        public const string GitHubComUrl = "https://api.github.com";
        private static readonly LazyRegex NextPageLinkPattern = new("<(?<uri>[^>]+)>; rel=\"next\"", RegexOptions.Compiled);
        private static readonly string[] EnabledPreviews = new[]
        {
            "application/vnd.github.inertia-preview+json", // projects
        };
        private readonly string apiBaseUrl;

#if NET452
        static GitHubClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
#endif

        public GitHubClient(string apiBaseUrl, string userName, SecureString password, string organizationName)
        {
            if (!string.IsNullOrEmpty(userName) && password == null)
                throw new InvalidOperationException("If a username is specified, a password must be specified in the operation or in the resource credential.");

            this.apiBaseUrl = AH.CoalesceString(apiBaseUrl, GitHubClient.GitHubComUrl).TrimEnd('/');
            this.UserName = userName;
            this.Password = password;
            this.OrganizationName = AH.NullIf(organizationName, string.Empty);
        }
        public GitHubClient(GitHubSecureCredentials credentials, GitHubSecureResource resource)
        {
            this.apiBaseUrl = AH.CoalesceString(resource?.ApiUrl, GitHubComUrl).TrimEnd('/');
            this.UserName = credentials?.UserName;
            this.Password = credentials?.Password;
            this.OrganizationName = AH.NullIf(resource?.OrganizationName, string.Empty);
        }

        public string OrganizationName { get; }
        public string UserName { get; }
        public SecureString Password { get; }

        public async Task<IList<JObject>> GetOrganizationsAsync(CancellationToken cancellationToken)
        {
            var results = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/user/orgs?per_page=100", cancellationToken).ConfigureAwait(false);
            return results.Cast<JObject>().ToList();
        }
        public async Task<IList<JObject>> GetRepositoriesAsync(CancellationToken cancellationToken)
        {
            string url;
            if (!string.IsNullOrEmpty(this.OrganizationName))
                url = $"{this.apiBaseUrl}/orgs/{Esc(this.OrganizationName)}/repos?per_page=100";
            else
                url = $"{this.apiBaseUrl}/user/repos?per_page=100";

            IEnumerable<object> results;
            try
            {
                results = await this.InvokePagesAsync("GET", url, cancellationToken).ConfigureAwait(false);
            }
            catch when (!string.IsNullOrEmpty(this.OrganizationName))
            {
                url = $"{this.apiBaseUrl}/users/{Esc(this.OrganizationName)}/repos?per_page=100";
                results = await this.InvokePagesAsync("GET", url, cancellationToken).ConfigureAwait(false);
            }
            return results.Cast<JObject>().ToList();
        }
        public async Task<IList<JObject>> GetIssuesAsync(string ownerName, string repositoryName, GitHubIssueFilter filter, CancellationToken cancellationToken)
        {
            var issues = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/issues{filter.ToQueryString()}", cancellationToken).ConfigureAwait(false);
            return issues.Cast<JObject>().ToList();
        }

        public async Task<JObject> GetIssueAsync(string issueUrl, CancellationToken cancellationToken = default)
        {
            var issue = await this.InvokeAsync("GET", issueUrl, cancellationToken).ConfigureAwait(false);
            return (JObject)issue;
        }
        public async Task<int> CreateIssueAsync(string ownerName, string repositoryName, object data, CancellationToken cancellationToken)
        {
            var issue = (JObject)await this.InvokeAsync("POST", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/issues", data, cancellationToken).ConfigureAwait(false);
            return int.TryParse(issue["number"]?.ToString(), out int number) ? number : 0;
        }
        public Task UpdateIssueAsync(int issueId, string ownerName, string repositoryName, object update, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("PATCH", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/issues/{issueId}", update, cancellationToken);
        }

        public async Task<int> CreateMilestoneAsync(string milestone, string ownerName, string repositoryName, CancellationToken cancellationToken)
        {
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, ownerName, repositoryName, cancellationToken).ConfigureAwait(false);
            if (milestoneNumber.HasValue)
                return milestoneNumber.Value;

            var data = (JObject)await this.InvokeAsync("POST", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/milestones", new { title = milestone }, cancellationToken).ConfigureAwait(false);
            return int.TryParse(data["number"]?.ToString(), out int number) ? number : 0;
        }
        public async Task CloseMilestoneAsync(string milestone, string ownerName, string repositoryName, CancellationToken cancellationToken)
        {
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, ownerName, repositoryName, cancellationToken).ConfigureAwait(false);
            if (milestoneNumber == null)
                return;

            await this.InvokeAsync("PATCH", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/milestones/{milestoneNumber}", new { state = "closed" }, cancellationToken).ConfigureAwait(false);
        }

        public Task UpdateMilestoneAsync(int milestoneNumber, string ownerName, string repositoryName, object data, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("PATCH", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/milestones/{milestoneNumber}", data, cancellationToken);
        }

        public Task CreateStatusAsync(string ownerName, string repositoryName, string commitHash, string state, string target_url, string description, string context, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("POST", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/statuses/{Esc(commitHash)}", new { state, target_url, description, context }, cancellationToken);
        }

        public Task CreateCommentAsync(int issueId, string ownerName, string repositoryName, string commentText, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("POST", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/issues/{issueId}/comments", new { body = commentText }, cancellationToken);
        }

        public async Task<int?> FindMilestoneAsync(string title, string ownerName, string repositoryName, CancellationToken cancellationToken)
        {
            var milestones = await this.GetMilestonesAsync(ownerName, repositoryName, "all", cancellationToken).ConfigureAwait(false);
            return milestones
                .Where(m => string.Equals(m["title"]?.ToString() ?? string.Empty, title, StringComparison.OrdinalIgnoreCase))
                .Select(m => int.TryParse(m["number"]?.ToString(), out int number) ? number : (int?)null)
                .FirstOrDefault();
        }

        public async Task<IList<JObject>> GetMilestonesAsync(string ownerName, string repositoryName, string state, CancellationToken cancellationToken)
        {
            var milestones = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/milestones?state={Uri.EscapeDataString(state)}&sort=due_on&direction=desc&per_page=100", cancellationToken).ConfigureAwait(false);
            if (milestones == null)
                return new JObject[0];

            return milestones.Cast<JObject>().ToList();
        }

        public async Task<IList<JObject>> GetProjectsAsync(string ownerName, string repositoryName, CancellationToken cancellationToken)
        {
            var url = $"{this.apiBaseUrl}/orgs/{Esc(ownerName)}/projects?state=all";
            if (!string.IsNullOrEmpty(repositoryName))
                url = $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/projects?state=all";

            var projects = await this.InvokePagesAsync("GET", url, cancellationToken);
            if (projects == null)
                return new JObject[0];

            return projects.Cast<JObject>().ToList();
        }

        internal async Task<IList<KeyValuePair<string, IList<JObject>>>> GetProjectColumnsAsync(string projectColumnsUrl, CancellationToken cancellationToken)
        {
            var columnData = await this.InvokePagesAsync("GET", projectColumnsUrl, cancellationToken);
            if (columnData == null)
                return new KeyValuePair<string, IList<JObject>>[0];

            var columns = new List<KeyValuePair<string, IList<JObject>>>();
            foreach (var column in columnData.Cast<Dictionary<string, object>>())
            {
                var cardData = await this.InvokePagesAsync("GET", (string)column["cards_url"], cancellationToken);
                var cards = cardData?.Cast<JObject>().ToArray() ?? new JObject[0];
                columns.Add(new KeyValuePair<string, IList<JObject>>((string)column["name"], cards.ToList()));
            }

            return columns;
        }

        public async Task<JObject> GetReleaseAsync(string ownerName, string repositoryName, string tag, CancellationToken cancellationToken)
        {
            try
            {
                var releases = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/releases", cancellationToken).ConfigureAwait(false);
                return releases.Cast<JObject>().SingleOrDefault(r => string.Equals((string)r["tag_name"], tag));
            }
            catch (Exception ex) when (((ex.InnerException as WebException)?.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<IEnumerable<string>> ListRefsAsync(string ownerName, string repositoryName, RefType? type, CancellationToken cancellationToken)
        {
            var prefix = AH.Switch<RefType?, string>(type)
                .Case(null, "refs")
                .Case(RefType.Branch, "refs/heads")
                .Case(RefType.Tag, "refs/tags")
                .End();

            var refs = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/git/{prefix}", cancellationToken).ConfigureAwait(false);

            return refs.Cast<JObject>().Select(r => trim((string)r["ref"]));

            string trim(string s)
            {
                if (s.StartsWith(prefix))
                    s = s.Substring(prefix.Length);

                if (s.StartsWith("/"))
                    s = s.Substring(1);

                return s;
            }
        }

        public async Task<object> EnsureReleaseAsync(string ownerName, string repositoryName, string tag, string target, string name, string body, bool? draft, bool? prerelease, CancellationToken cancellationToken)
        {
            var data = new Dictionary<string, object> { ["tag_name"] = tag };
            if (target != null)
                data["target_commitish"] = target;
            if (name != null)
                data["name"] = name;
            if (body != null)
                data["body"] = body;
            if (draft.HasValue)
                data["draft"] = draft.Value;
            if (prerelease.HasValue)
                data["prerelease"] = prerelease.Value;

            var existingRelease = await this.GetReleaseAsync(ownerName, repositoryName, tag, cancellationToken).ConfigureAwait(false);
            if (existingRelease != null)
                return await this.InvokeAsync("PATCH", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/releases/{Esc(existingRelease["id"])}", data, cancellationToken).ConfigureAwait(false);

            return await this.InvokeAsync("POST", $"{this.apiBaseUrl}/repos/{Esc(ownerName)}/{Esc(repositoryName)}/releases", data, cancellationToken).ConfigureAwait(false);
        }

        public async Task<object> UploadReleaseAssetAsync(string ownerName, string repositoryName, string tag, string name, string contentType, Stream contents, Action<long> reportProgress, CancellationToken cancellationToken)
        {
            var release = await this.GetReleaseAsync(ownerName, repositoryName, tag, cancellationToken).ConfigureAwait(false);
            if (release == null)
                throw new ExecutionFailureException($"No release found with tag {tag} in repository {ownerName}/{repositoryName}");

            string uploadUrl = FormatTemplateUri((string)release["upload_url"], name);

            var request = this.CreateRequest("POST", uploadUrl);
            request.ContentType = contentType;
            request.AllowWriteStreamBuffering = false;
            if (contents.CanSeek)
                request.ContentLength = contents.Length - contents.Position;
            else
                request.SendChunked = true;

            using (cancellationToken.Register(() => request.Abort()))
            {
                using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await contents.CopyToAsync(requestStream, 81920, cancellationToken, reportProgress).ConfigureAwait(false);
                }

                try
                {
                    using var response = await request.GetResponseAsync().ConfigureAwait(false);
                    using var reader = new StreamReader(response.GetResponseStream(), InedoLib.UTF8Encoding);

                    string responseText = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var responseJson = JsonConvert.DeserializeObject(responseText);
                    return responseJson;
                }
                catch (WebException ex) when (ex.Status == WebExceptionStatus.RequestCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
                {
                    throw GetErrorResponseException(errorResponse);
                }
            }
        }

        private static string FormatTemplateUri(string templateUri, string name)
        {
            // quick hack for URI templates since former NuGet package doesn't support target framework v4.5.2 
            // The format of templatedUploadUri is: https://host/repos/org/repoName/releases/1000/assets{?name,label}

            int index = templateUri.IndexOf('{');
            return templateUri.Substring(0, index) + "?name=" + Uri.EscapeDataString(name);
        }
        private static string Esc(string part) => Uri.EscapeUriString(part ?? string.Empty);
        private static string Esc(object part) => Esc(part?.ToString());

        private async Task<IEnumerable<object>> InvokePagesAsync(string method, string uri, CancellationToken cancellationToken)
        {
            return (IEnumerable<object>)await this.InvokeAsync(method, uri, null, true, cancellationToken).ConfigureAwait(false);
        }
        private Task<object> InvokeAsync(string method, string uri, CancellationToken cancellationToken)
        {
            return this.InvokeAsync(method, uri, null, false, cancellationToken);
        }
        private Task<object> InvokeAsync(string method, string uri, object data, CancellationToken cancellationToken)
        {
            return this.InvokeAsync(method, uri, data, false, cancellationToken);
        }
        private async Task<object> InvokeAsync(string method, string uri, object data, bool allPages, CancellationToken cancellationToken)
        {
            var request = this.CreateRequest(method, uri);

            using (cancellationToken.Register(() => request.Abort()))
            {
                if (data != null)
                {
                    using var writer = new StreamWriter(await request.GetRequestStreamAsync().ConfigureAwait(false), InedoLib.UTF8Encoding);
                    await writer.WriteAsync(JsonConvert.SerializeObject(data)).ConfigureAwait(false);
                }

                string linkHeader;
                object responseJson;

                try
                {
                    using var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                    linkHeader = response.Headers["Link"] ?? string.Empty;

                    using var reader = new StreamReader(response.GetResponseStream(), InedoLib.UTF8Encoding);
                    string responseText = await reader.ReadToEndAsync().ConfigureAwait(false);
                    responseJson = JsonConvert.DeserializeObject(responseText);
                }
                catch (WebException ex) when (ex.Status == WebExceptionStatus.RequestCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
                {
                    throw GetErrorResponseException(errorResponse);
                }

                if (allPages)
                {
                    var nextPage = NextPageLinkPattern.Match(linkHeader);
                    if (nextPage.Success)
                        responseJson = ((IEnumerable<object>)responseJson).Concat((IEnumerable<object>)await this.InvokeAsync(method, nextPage.Groups["uri"].Value, data, true, cancellationToken).ConfigureAwait(false));
                }

                return responseJson;
            }
        }
        private HttpWebRequest CreateRequest(string method, string uri)
        {
            var request = WebRequest.CreateHttp(uri);
            request.UserAgent = "InedoGitHubExtension/" + typeof(GitHubClient).Assembly.GetName().Version.ToString();
            request.Method = method;
            request.Accept = string.Join(", ", new[] { "application/vnd.github.v3+json" }.Concat(EnabledPreviews));

            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers[HttpRequestHeader.Authorization] = "token " + AH.Unprotect(this.Password);

            return request;
        }
        private static Exception GetErrorResponseException(HttpWebResponse response)
        {
            using var reader = new StreamReader(response.GetResponseStream(), InedoLib.UTF8Encoding);
            var errorMessage = $"Server replied with {(int)response.StatusCode}";

            if (response.ContentType?.StartsWith("application/json") == true)
            {
                var obj = JObject.Load(new JsonTextReader(reader));

                var parsedMessage = (string)obj?.Property("message");
                if (!string.IsNullOrWhiteSpace(parsedMessage))
                    errorMessage += ": " + parsedMessage;

                if (obj?.Property("errors")?.Value is JArray errorsArray && errorsArray.Count > 0)
                {
                    var moreDetails = errorsArray.OfType<JObject>().Select(o => (string)o.Property("resource") + " " + (string)o.Property("code")).ToList();
                    if (moreDetails.Count > 0)
                        errorMessage += " (" + string.Join(", ", moreDetails) + ")";
                }
            }
            else
            {
                var details = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(details))
                    errorMessage += ": " + details;
            }

            return new ExecutionFailureException(errorMessage);
        }
    }
}
