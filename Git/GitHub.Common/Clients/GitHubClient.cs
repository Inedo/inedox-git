using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Inedo.Extensions.Clients
{
    internal sealed class GitHubClient
    {
        public const string GitHubComUrl = "https://api.github.com";

        private string apiBaseUrl;

        public GitHubClient(string apiBaseUrl, string userName, SecureString password, string organizationName)
        {
            this.apiBaseUrl = AH.CoalesceString(apiBaseUrl, GitHubClient.GitHubComUrl).TrimEnd('/');
            this.UserName = userName;
            this.Password = password;
            this.OrganizationName = AH.NullIf(organizationName, string.Empty);
        }

        public string OrganizationName { get; }
        public string UserName { get; }
        public SecureString Password { get; }

        public async Task<IList<Dictionary<string, object>>> GetRepositoriesAsync()
        {
            UriBuilder url;
            if (!string.IsNullOrEmpty(this.OrganizationName))
                url = new UriBuilder($"{this.apiBaseUrl}/orgs/{Uri.EscapeUriString(this.OrganizationName)}/repos?per_page=500");
            else
                url = new UriBuilder($"{this.apiBaseUrl}/user/repos?per_page=500");

            var results = (IEnumerable<object>)await this.InvokeAsync("GET", url.ToString()).ConfigureAwait(false);
            return results.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<IList<Dictionary<string, object>>> GetIssuesAsync(string ownerName, string repositoryName, GitHubIssueFilter filter)
        {
            var issues = (IEnumerable<object>)await this.InvokeAsync("GET", string.Format("{0}/repos/{1}/{2}/issues{3}", this.apiBaseUrl, ownerName, repositoryName, filter.ToQueryString())).ConfigureAwait(false);
            return issues.Cast<Dictionary<string, object>>().ToList();
        }
        
        public async Task<Dictionary<string, object>> GetIssueAsync(string issueId, string ownerName, string repositoryName)
        {
            var issue = await this.InvokeAsync("GET", string.Format("{0}/repos/{1}/{2}/issues/{3}", this.apiBaseUrl, ownerName, repositoryName, issueId)).ConfigureAwait(false);
            return (Dictionary<string, object>)issue;
        }
        public Task UpdateIssueAsync(string issueId, string ownerName, string repositoryName, object update)
        {
            return this.InvokeAsync("PATCH", string.Format("{0}/repos/{1}/{2}/issues/{3}", this.apiBaseUrl, ownerName, repositoryName, issueId), update);
        }

        public async Task CreateMilestoneAsync(string milestone, string ownerName, string repositoryName)
        {
            var url = string.Format("{0}/repos/{1}/{2}/milestones", this.apiBaseUrl, ownerName, repositoryName);
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, ownerName, repositoryName).ConfigureAwait(false);
            if (milestoneNumber != null)
                return;

            await this.InvokeAsync(
                "POST",
                url,
                new { title = milestone }
            ).ConfigureAwait(false);
        }
        public async Task CloseMilestoneAsync(string milestone, string ownerName, string repositoryName)
        {
            var url = string.Format("{0}/repos/{1}/{2}/milestones", this.apiBaseUrl, ownerName, repositoryName);
            int? milestoneNumber = await this.FindMilestoneAsync(milestone, ownerName, repositoryName).ConfigureAwait(false);
            if (milestoneNumber == null)
                return;

            await this.InvokeAsync(
                "PATCH",
                url + "/" + milestoneNumber,
                new { state = "closed" }
            ).ConfigureAwait(false);
        }

        public Task CreateCommentAsync(string issueId, string ownerName, string repositoryName, string commentText)
        {
            return this.InvokeAsync(
                "POST",
                string.Format("{0}/repos/{1}/{2}/issues/{3}/comments", this.apiBaseUrl, Uri.EscapeDataString(ownerName), Uri.EscapeDataString(repositoryName), Uri.EscapeDataString(issueId)),
                new
                {
                    body = commentText
                }
            );
        }

        public async Task<int?> FindMilestoneAsync(string title, string ownerName, string repositoryName)
        {
            var milestones = await this.GetMilestonesAsync(ownerName, repositoryName, "all").ConfigureAwait(false);

            return milestones
                .Where(m => string.Equals((m["title"] ?? "").ToString(), title, StringComparison.OrdinalIgnoreCase))
                .Select(m => m["number"] as int?)
                .FirstOrDefault();
        }

        public async Task<IList<Dictionary<string, object>>> GetMilestonesAsync(string ownerName, string repositoryName, string state)
        {
            var milestones = (IEnumerable<object>)await this.InvokeAsync("GET", string.Format("{0}/repos/{1}/{2}/milestones?state={3}&sort=due_on&direction=desc", this.apiBaseUrl, ownerName, repositoryName, state)).ConfigureAwait(false);
            if (milestones == null)
                return new List<Dictionary<string, object>>();

            return milestones.Cast<Dictionary<string, object>>().ToList();                
        }

        private Task<object> InvokeAsync(string method, string url)
        {
            return this.InvokeAsync(method, url, null);
        }
        private async Task<object> InvokeAsync(string method, string url, object data)
        {
            var request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.UserAgent = "BuildMasterGitHubExtension/" + typeof(GitHubClient).Assembly.GetName().Version.ToString();
            request.Method = method;
            if (data != null)
            {
                using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                using (var writer = new StreamWriter(requestStream, InedoLib.UTF8Encoding))
                {
                    InedoLib.Util.JavaScript.WriteJson(writer, data);
                }
            }
            
            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers[HttpRequestHeader.Authorization] = "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(this.UserName + ":" + this.Password.ToUnsecureString()));
            
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    var js = new JavaScriptSerializer();
                    string responseText = await reader.ReadToEndAsync().ConfigureAwait(false);
                    return js.DeserializeObject(responseText);
                }
            }
            catch (WebException ex) when (ex.Response != null)
            {
                using (var responseStream = ex.Response.GetResponseStream())
                {
                    string message = await new StreamReader(responseStream).ReadToEndAsync().ConfigureAwait(false);
                    throw new Exception(message);
                }
            }
        }
    }

    internal sealed class GitHubIssueFilter
    {
        private string milestone;

        public string Milestone
        {
            get { return this.milestone; }
            set
            {
                if (value != null && AH.ParseInt(value) == null && value != "*" && !string.Equals("none", value, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("milestone must be an integer, or a string of '*' or 'none'.");

                this.milestone = value;
            }
        }
        public string Labels { get; set; }
        public string CustomFilterQueryString { get; set; }

        public string ToQueryString()
        {
            if (!string.IsNullOrEmpty(this.CustomFilterQueryString))
                return this.CustomFilterQueryString;

            var buffer = new StringBuilder(128);
            if (!string.IsNullOrEmpty(this.Milestone))
                buffer.Append("milestone=" + Uri.EscapeDataString(this.Milestone));
            if (!string.IsNullOrEmpty(this.Labels))
                buffer.Append("labels=" + Uri.EscapeDataString(this.Labels));

            if (buffer.Length > 0)
                return "?" + buffer.ToString();
            else
                return string.Empty;
        }
    }
}
