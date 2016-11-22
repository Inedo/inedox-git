using System.Security;
using Inedo.Serialization;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions;
using Inedo.Otter.Web.Controls.Extensions;
#endif


namespace Inedo.Extensions.Credentials
{
    [ScriptAlias("GitHub")]
    [DisplayName("GitHub")]
    [Description("Credentials for GitHub.")]
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
        public string OrganizationName { get; set; }

        [Persistent]
        [DisplayName("Repository")]
        [PlaceholderText("e.g. log4net")]
        public string RepositoryName { get; set; }
        
        [Persistent]
        [Undisclosed]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string RepositoryUrl { get; set; }

        public override RichDescription GetDescription()
        {
            var desc = new RichDescription(this.UserName, "@", "GitHub");
            if (!string.IsNullOrEmpty(this.OrganizationName))
                desc.AppendContent(",Organization=", this.OrganizationName);

            return desc;            
        }
    }
}
