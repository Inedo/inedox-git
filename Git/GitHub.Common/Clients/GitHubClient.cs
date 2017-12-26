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
    internal sealed class GitHubClient
    {
        public const string GitHubComUrl = "https://api.github.com";

        private string apiBaseUrl;

        public GitHubClient(string apiBaseUrl, string userName, SecureString password, string organizationName)
        {
            if (!string.IsNullOrEmpty(userName) && password == null)
                throw new InvalidOperationException("If a username is specified, a password must be specified in the operation or in the resource credential.");

            this.apiBaseUrl = AH.CoalesceString(apiBaseUrl, GitHubClient.GitHubComUrl).TrimEnd('/');
            this.UserName = userName;
            this.Password = password;
            this.OrganizationName = AH.NullIf(organizationName, string.Empty);
        }

        public string OrganizationName { get; }
        public string UserName { get; }
        public SecureString Password { get; }

        public async Task<IList<Dictionary<string, object>>> GetOrganizationsAsync()
        {
            var results = await this.InvokePagesAsync("GET", $"{this.apiBaseUrl}/user/orgs?per_page=100").ConfigureAwait(false);
            return results.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<IList<Dictionary<string, object>>> GetRepositoriesAsync()
        {
            string url;
            if (!string.IsNullOrEmpty(this.OrganizationName))
                url = $"{this.apiBaseUrl}/users/{Uri.EscapeUriString(this.OrganizationName)}/repos?per_page=100";
            else
                url = $"{this.apiBaseUrl}/user/repos?per_page=100";

            var results = await this.InvokePagesAsync("GET", url).ConfigureAwait(false);
            return results.Cast<Dictionary<string, object>>().ToList();
        }

        public async Task<IList<Dictionary<string, object>>> GetIssuesAsync(string ownerName, string repositoryName, GitHubIssueFilter filter)
        {
            var issues = await this.InvokePagesAsync("GET", string.Format("{0}/repos/{1}/{2}/issues{3}", this.apiBaseUrl, ownerName, repositoryName, filter.ToQueryString())).ConfigureAwait(false);
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
            var milestones = await this.InvokePagesAsync("GET", string.Format("{0}/repos/{1}/{2}/milestones?state={3}&sort=due_on&direction=desc&per_page=100", this.apiBaseUrl, ownerName, repositoryName, state)).ConfigureAwait(false);
            if (milestones == null)
                return new List<Dictionary<string, object>>();

            return milestones.Cast<Dictionary<string, object>>().ToList();                
        }

        public async Task<Dictionary<string, object>> GetReleaseAsync(string ownerName, string repositoryName, string tag)
        {
            try
            {
                var releases = await this.InvokePagesAsync("GET", string.Format("{0}/repos/{1}/{2}/releases", this.apiBaseUrl, ownerName, repositoryName)).ConfigureAwait(false);
                return releases.Cast<Dictionary<string, object>>().SingleOrDefault(r => string.Equals(r["tag_name"], tag));
            }
            catch (Exception ex) when (((ex.InnerException as WebException)?.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<object> EnsureReleaseAsync(string ownerName, string repositoryName, string tag, string target = null, string name = null, string body = null, bool? draft = null, bool? prerelease = null)
        {
            var data = new Dictionary<string, object>();
            data["tag_name"] = tag;
            if (target != null)
            {
                data["target_commitish"] = target;
            }
            if (name != null)
            {
                data["name"] = name;
            }
            if (body != null)
            {
                data["body"] = body;
            }
            if (draft.HasValue)
            {
                data["draft"] = draft.Value;
            }
            if (prerelease.HasValue)
            {
                data["prerelease"] = prerelease.Value;
            }

            var existingRelease = await this.GetReleaseAsync(ownerName, repositoryName, tag).ConfigureAwait(false);
            if (existingRelease != null)
            {
                return await this.InvokeAsync("PATCH", string.Format("{0}/repos/{1}/{2}/releases/{3}", this.apiBaseUrl, ownerName, repositoryName, existingRelease["id"]), data).ConfigureAwait(false);
            }
            return await this.InvokeAsync("POST", string.Format("{0}/repos/{1}/{2}/releases", this.apiBaseUrl, ownerName, repositoryName), data).ConfigureAwait(false);
        }

        public async Task<object> UploadReleaseAssetAsync(string ownerName, string repositoryName, string tag, string name, string contentType, Stream contents)
        {
            var release = await this.GetReleaseAsync(ownerName, repositoryName, tag).ConfigureAwait(false);
            if (release == null)
            {
                throw new ArgumentException($"No release found with tag {tag} in repository {ownerName}/{repositoryName}", nameof(tag));
            }

            var uploadUriTemplate = new UriTemplate.Core.UriTemplate((string)release["upload_url"]);
            var uploadUri = uploadUriTemplate.BindByName(new Dictionary<string, string> { { "name", name } });

            var request = WebRequest.CreateHttp(uploadUri);
            request.UserAgent = "BuildMasterGitHubExtension/" + typeof(GitHubClient).Assembly.GetName().Version.ToString();
            request.Method = "POST";

            request.ContentType = contentType;
            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers[HttpRequestHeader.Authorization] = "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(this.UserName + ":" + this.Password.ToUnsecureString()));

            using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                await contents.CopyToAsync(requestStream).ConfigureAwait(false);
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
            request.UserAgent = "BuildMasterGitHubExtension/" + typeof(GitHubClient).Assembly.GetName().Version.ToString();
            request.Method = method;

            if (!string.IsNullOrEmpty(this.UserName))
                request.Headers[HttpRequestHeader.Authorization] = "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(this.UserName + ":" + this.Password.ToUnsecureString()));

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
            buffer.Append("?state=all");
            if (!string.IsNullOrEmpty(this.Milestone))
                buffer.Append("&milestone=" + Uri.EscapeDataString(this.Milestone));
            if (!string.IsNullOrEmpty(this.Labels))
                buffer.Append("&labels=" + Uri.EscapeDataString(this.Labels));
            buffer.Append("&per_page=100");

            return buffer.ToString();
        }
    }
}
