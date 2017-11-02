using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Inedo.Extensions.Clients
{
    internal sealed class GitLabClient
    {
        public const string GitLabComUrl = "https://gitlab.com/api";

        private string apiBaseUrl;

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

        public async Task<IList<Dictionary<string, object>>> GetGroupsAsync()
        {
            var results = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/v4/groups?per_page=100").ConfigureAwait(false);
            return results.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<IList<Dictionary<string, object>>> GetProjectsAsync()
        {
            UriBuilder url;
            if (!string.IsNullOrEmpty(this.GroupName))
                url = new UriBuilder($"{this.apiBaseUrl}/v4/groups/{Uri.EscapeUriString(this.GroupName)}/projects?per_page=100");
            else
                url = new UriBuilder($"{this.apiBaseUrl}/v4/projects?owned=true&per_page=100");

            var results = await this.InvokePagesAsync("GET", url.ToString()).ConfigureAwait(false);
            return results.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<Dictionary<string, object>> GetProjectAsync(string projectName)
        {
            try
            {
                var project = await this.InvokeAsync("GET", $"{this.apiBaseUrl}/v4/projects/{Uri.EscapeUriString(projectName)}").ConfigureAwait(false);
                return (Dictionary<string, object>)project;
            }
            catch (Exception ex) when (ex.Message == @"{""message"":""404 Project Not Found""}")
            {
                return null;
            }
        }

        public async Task<IList<Dictionary<string, object>>> GetIssuesAsync(string repositoryName, GitLabIssueFilter filter)
        {
            var issues = await this.InvokePagesAsync("GET", string.Format("{0}/v4/projects/{1}/issues{2}", this.apiBaseUrl, Uri.EscapeUriString(repositoryName), filter.ToQueryString())).ConfigureAwait(false);
            return issues.Cast<Dictionary<string, object>>().ToList();
        }
        
        public async Task<Dictionary<string, object>> GetIssueAsync(string issueId, string repositoryName)
        {
            var issue = await this.InvokeAsync("GET", string.Format("{0}/v4/projects/{1}/issues/{2}", this.apiBaseUrl, Uri.EscapeUriString(repositoryName), issueId)).ConfigureAwait(false);
            return (Dictionary<string, object>)issue;
        }
        public Task UpdateIssueAsync(string issueId, string repositoryName, object update)
        {
            return this.InvokeAsync("PUT", string.Format("{0}/v4/projects/{1}/issues/{2}", this.apiBaseUrl, Uri.EscapeUriString(repositoryName), issueId), update);
        }

        public async Task CreateMilestoneAsync(string milestone, string repositoryName)
        {
            var url = string.Format("{0}/v4/projects/{1}/milestones", this.apiBaseUrl, Uri.EscapeUriString(repositoryName));
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, repositoryName).ConfigureAwait(false);
            if (milestoneNumber != null)
                return;

            await this.InvokeAsync(
                "POST",
                url,
                new { title = milestone }
            ).ConfigureAwait(false);
        }
        public async Task CloseMilestoneAsync(string milestone, string repositoryName)
        {
            var url = string.Format("{0}/v4/projects/{1}/milestones", this.apiBaseUrl, Uri.EscapeUriString(repositoryName));
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, repositoryName).ConfigureAwait(false);
            if (milestoneNumber == null)
                return;

            await this.InvokeAsync(
                "PUT",
                url + "/" + milestoneNumber,
                new { state_event = "close" }
            ).ConfigureAwait(false);
        }

        public Task CreateCommentAsync(string issueId, string repositoryName, string commentText)
        {
            return this.InvokeAsync(
                "POST",
                string.Format("{0}/v4/projects/{1}/issues/{2}/notes", this.apiBaseUrl, Uri.EscapeDataString(repositoryName), Uri.EscapeDataString(issueId)),
                new
                {
                    body = commentText
                }
            );
        }

        public async Task<int?> FindMilestoneAsync(string title, string repositoryName)
        {
            var milestones = await this.GetMilestonesAsync(repositoryName, null).ConfigureAwait(false);

            return milestones
                .Where(m => string.Equals((m["title"] ?? "").ToString(), title, StringComparison.OrdinalIgnoreCase))
                .Select(m => m["id"] as int?)
                .FirstOrDefault();
        }

        public async Task<IList<Dictionary<string, object>>> GetMilestonesAsync(string repositoryName, string state)
        {
            var milestones = await this.InvokePagesAsync("GET", string.Format("{0}/v4/projects/{1}/milestones?per_page=100{2}", this.apiBaseUrl, Uri.EscapeUriString(repositoryName), string.IsNullOrEmpty(state) ? "" : "&state=" + state)).ConfigureAwait(false);
            if (milestones == null)
                return new List<Dictionary<string, object>>();

            return milestones.Cast<Dictionary<string, object>>().ToList();                
        }

        private static LazyRegex NextPageLinkPattern = new LazyRegex("<(?<url>[^>]+)>; rel=\"next\"", RegexOptions.Compiled);

        private async Task<IEnumerable<object>> InvokePagesAsync(string method, string url)
        {
            return (IEnumerable<object>)await this.InvokeAsync(method, url, null, true).ConfigureAwait(false);
        }
        private Task<object> InvokeAsync(string method, string url)
        {
            return this.InvokeAsync(method, url, null);
        }
        private async Task<object> InvokeAsync(string method, string url, object data, bool allPages = false)
        {
            var request = WebRequest.CreateHttp(url);
            request.UserAgent = "BuildMasterGitLabExtension/" + typeof(GitLabClient).Assembly.GetName().Version.ToString();
            request.Method = method;

            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers["PRIVATE-TOKEN"] = this.Password.ToUnsecureString();

            if (data != null)
            {
                using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                using (var writer = new StreamWriter(requestStream, InedoLib.UTF8Encoding))
                {
                    InedoLib.Util.JavaScript.WriteJson(writer, data);
                }
            }

            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    var js = new JavaScriptSerializer();
                    string responseText = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var responseJson = js.DeserializeObject(responseText);
                    if (allPages)
                    {
                        var nextPage = NextPageLinkPattern.Match(response.Headers["Link"] ?? "");
                        if (nextPage.Success)
                        {
                            responseJson = ((IEnumerable<object>)responseJson).Concat((IEnumerable<object>)await this.InvokeAsync(method, nextPage.Groups["url"].Value, data, true).ConfigureAwait(false));
                        }
                    }
                    return responseJson;
                }
            }
            catch (WebException ex) when (ex.Response != null)
            {
                using (var responseStream = ex.Response.GetResponseStream())
                {
                    string message = await new StreamReader(responseStream).ReadToEndAsync().ConfigureAwait(false);
                    throw new Exception(message, ex);
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

            var buffer = new StringBuilder(128);
            if (!string.IsNullOrEmpty(this.Milestone))
                buffer.Append("&milestone=" + Uri.EscapeDataString(this.Milestone));
            if (!string.IsNullOrEmpty(this.Labels))
                buffer.Append("&labels=" + Uri.EscapeDataString(this.Labels));
            buffer.Append("&per_page=100");

            return "?" + buffer.ToString().TrimStart('&');
        }
    }
}
