using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Credentials
{
    [ScriptAlias("GitHub")]
    [DisplayName("GitHub")]
    [Description("Credentials for GitHub.")]
    [PersistFrom("Inedo.Extensions.Credentials.GitHubCredentials,GitHub")]
    public sealed class GitHubCredentials : GitCredentials
    {
        [Persistent]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        public string ApiUrl { get; set; }

        [Persistent]
        [DisplayName("Organization name")]
        [PlaceholderText("e.g. apache")]
        [SuggestableValue(typeof(CredentialsOrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Persistent]
        [DisplayName("Repository")]
        [PlaceholderText("e.g. log4net")]
        [SuggestableValue(typeof(CredentialsRepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }
        
        [Persistent]
        [Undisclosed]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string RepositoryUrl
        {
            get
            {
                var github = new GitHubClient(this.ApiUrl, this.UserName, this.Password, this.OrganizationName);

                var repo = (from r in github.GetRepositoriesAsync(CancellationToken.None).Result()
                            where string.Equals((string)r["name"], this.RepositoryName, StringComparison.OrdinalIgnoreCase)
                            select r).FirstOrDefault();

                if (repo == null)
                    throw new InvalidOperationException($"Repository '{this.RepositoryName}' not found on GitHub.");

                return (string)repo["clone_url"];
            }
            set
            {
            }
        }

        public override RichDescription GetDescription()
        {
            var desc = new RichDescription(AH.CoalesceString(this.UserName, "Anonymous"), "@", "GitHub");
            if (!string.IsNullOrEmpty(this.OrganizationName))
                desc.AppendContent(",Organization=", this.OrganizationName);

            return desc;            
        }
    }
}
