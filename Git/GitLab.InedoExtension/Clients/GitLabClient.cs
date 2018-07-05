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
using System.Web.Script.Serialization;

namespace Inedo.Extensions.GitLab.Clients
{
    internal sealed class GitLabClient
    {
        public const string GitLabComUrl = "https://gitlab.com/api";

        private string apiBaseUrl;
        private static readonly JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
        private static string Esc(string part) => Uri.EscapeUriString(part ?? string.Empty);
        private static string Esc(object part) => Esc(part?.ToString());

        public GitLabClient(string apiBaseUrl, string userName, SecureString password, string groupName)
        {
            if (!string.IsNullOrEmpty(userName) && password == null)
                throw new InvalidOperationException("If a username is specified, a personal access token must be specified in the operation or in the resource credential.");

            this.apiBaseUrl = AH.CoalesceString(apiBaseUrl, GitLabClient.GitLabComUrl).TrimEnd('/');
            this.UserName = userName;
            this.Password = password;
            this.GroupName = AH.NullIf(groupName, string.Empty);
        }

        public string GroupName { get; }
        public string UserName { get; }
        public SecureString Password { get; }

        public async Task<IList<Dictionary<string, object>>> GetGroupsAsync(CancellationToken cancellationToken)
        {
            var results = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/groups?per_page=100", cancellationToken).ConfigureAwait(false);
            return results.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<IList<Dictionary<string, object>>> GetProjectsAsync(CancellationToken cancellationToken)
        {
            string uri;
            if (!string.IsNullOrEmpty(this.GroupName))
                uri = $"{this.apiBaseUrl}/v4/groups/{Esc(this.GroupName)}/projects?per_page=100";
            else
                uri = $"{this.apiBaseUrl}/v4/projects?owned=true&per_page=100";

            var results = await this.InvokePagesAsync("GET", uri, cancellationToken).ConfigureAwait(false);
            return results.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<Dictionary<string, object>> GetProjectAsync(string projectName, CancellationToken cancellationToken)
        {
            try
            {
                var project = await this.InvokeAsync("GET", $"{this.apiBaseUrl}/v4/projects/{Esc(projectName)}", cancellationToken).ConfigureAwait(false);
                return (Dictionary<string, object>)project;
            }
            catch (Exception ex) when (ex.Message == @"{""message"":""404 Project Not Found""}")
            {
                return null;
            }
        }

        public async Task<IList<Dictionary<string, object>>> GetIssuesAsync(string repositoryName, GitLabIssueFilter filter, CancellationToken cancellationToken)
        {
            var issues = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/issues{filter.ToQueryString()}", cancellationToken).ConfigureAwait(false);
            return issues.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<Dictionary<string, object>> GetIssueAsync(string issueId, string repositoryName, CancellationToken cancellationToken)
        {
            var issue = await this.InvokeAsync("GET", $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/issues/{Esc(issueId)}", cancellationToken).ConfigureAwait(false);
            return (Dictionary<string, object>)issue;
        }
        public Task UpdateIssueAsync(string issueId, string repositoryName, object update, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("PUT", $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/issues/{Esc(issueId)}", update, cancellationToken);
        }

        public async Task CreateMilestoneAsync(string milestone, string repositoryName, CancellationToken cancellationToken)
        {
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, repositoryName, cancellationToken).ConfigureAwait(false);
            if (milestoneNumber != null)
                return;

            await this.InvokeAsync("POST", $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/milestones", new { title = milestone }, cancellationToken).ConfigureAwait(false);
        }
        public async Task CloseMilestoneAsync(string milestone, string repositoryName, CancellationToken cancellationToken)
        {
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, repositoryName, cancellationToken).ConfigureAwait(false);
            if (milestoneNumber == null)
                return;

            await this.InvokeAsync("PUT", $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/milestones/{Esc(milestoneNumber)}", new { state_event = "close" }, cancellationToken).ConfigureAwait(false);
        }

        public Task CreateCommentAsync(string issueId, string repositoryName, string commentText, CancellationToken cancellationToken)
        {
            return this.InvokeAsync("POST", $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/issues/{Esc(issueId)}/notes", new { body = commentText }, cancellationToken);
        }

        public async Task<int?> FindMilestoneAsync(string title, string repositoryName, CancellationToken cancellationToken)
        {
            var milestones = await this.GetMilestonesAsync(repositoryName, null, cancellationToken).ConfigureAwait(false);

            return milestones
                .Where(m => string.Equals(m["title"]?.ToString() ?? string.Empty, title, StringComparison.OrdinalIgnoreCase))
                .Select(m => m["id"] as int?)
                .FirstOrDefault();
        }

        public async Task<IList<Dictionary<string, object>>> GetMilestonesAsync(string repositoryName, string state, CancellationToken cancellationToken)
        {
            var milestones = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/milestones?per_page=100{(string.IsNullOrEmpty(state) ? string.Empty : "&state=" + Uri.EscapeDataString(state))}", cancellationToken).ConfigureAwait(false);
            if (milestones == null)
                return new Dictionary<string, object>[0];

            return milestones.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<Dictionary<string, object>> GetTagAsync(string repositoryName, string tag, CancellationToken cancellationToken)
        {
            try
            {
                var tagData = await this.InvokeAsync("GET", $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/repository/tags/{Esc(tag)}", cancellationToken).ConfigureAwait(false);
                return (Dictionary<string, object>)tagData;
            }
            catch (Exception ex) when (ex.Message == @"{""message"":""404 Tag Not Found""}")
            {
                return null;
            }
        }

        public async Task EnsureReleaseAsync(string repositoryName, string tag, string description, CancellationToken cancellationToken)
        {
            var uri = $"{this.apiBaseUrl}/v4/projects/{Esc(repositoryName)}/repository/tags/{Esc(tag)}/release";
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

        private static LazyRegex NextPageLinkPattern = new LazyRegex("<(?<uri>[^>]+)>; rel=\"next\"", RegexOptions.Compiled);

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
                        await writer.WriteAsync(jsonSerializer.Serialize(data)).ConfigureAwait(false);
                    }
                }

                try
                {
                    using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                    using (var responseStream = response.GetResponseStream())
                    using (var reader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                    {
                        string responseText = await reader.ReadToEndAsync().ConfigureAwait(false);
                        var responseJson = jsonSerializer.DeserializeObject(responseText);
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
                    using (var responseStream = ex.Response.GetResponseStream())
                    using (var reader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                    {
                        string message = await reader.ReadToEndAsync().ConfigureAwait(false);
                        throw new Exception(message, ex);
                    }
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
}
