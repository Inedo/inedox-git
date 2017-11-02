using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security;
using System.Threading.Tasks;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Web.Controls;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Upload GitHub Release Assets")]
    [Description("Uploads files as attachments to a GitHub release.")]
    [Tag("source-control")]
    [ScriptAlias("GitHub-Upload-Release-Assets")]
    [ScriptNamespace("GitHub", PreferUnqualified = true)]
    public sealed class GitHubUploadReleaseAssetsOperation : ExecuteOperation, IHasCredentials<GitHubCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.UserName))]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.Password))]
        public SecureString Password { get; set; }

        [Category("GitHub")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [MappedCredential(nameof(GitHubCredentials.OrganizationName))]
        [PlaceholderText("Use organization from credentials")]
        [SuggestibleValue(typeof(OrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Category("GitHub")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [MappedCredential(nameof(GitHubCredentials.RepositoryName))]
        [PlaceholderText("Use repository from credentials")]
        [SuggestibleValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        [MappedCredential(nameof(GitHubCredentials.ApiUrl))]
        public string ApiUrl { get; set; }

        [Required]
        [ScriptAlias("Tag")]
        [DisplayName("Tag")]
        [Description("The tag associated with the release. The release must already exist.")]
        public string Tag { get; set; }

        [Required]
        [ScriptAlias("Include")]
#if Hedgehog
        [MaskingDescription]
#else
        [Description(CommonDescriptions.MaskingHelp)]
#endif
        [PlaceholderText("* (top-level items)")]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
#if Hedgehog
        [MaskingDescription]
#else
        [Description(CommonDescriptions.MaskingHelp)]
#endif
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("Directory")]
        public string SourceDirectory { get; set; }

        [ScriptAlias("ContentType")]
        [DisplayName("Content type")]
        [Description(@"The content type of the assets. For a list of acceptable types, see the IANA list of <a href=""https://www.iana.org/assignments/media-types/media-types.xhtml"">media types</a>.")]
        [Example("application/zip")]
        [DefaultValue("application/octet-stream")]
        public string ContentType { get; set; } = "application/octet-stream";

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var sourceDirectory = context.ResolvePath(this.SourceDirectory);

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var files = await fileOps.GetFileSystemInfosAsync(sourceDirectory, new MaskingContext(this.Includes, this.Excludes)).ConfigureAwait(false);
            if (files.Count == 0)
            {
                this.LogWarning("No files matched.");
                return;
            }

            var github = new GitHubClient(this.ApiUrl, this.UserName, this.Password, this.OrganizationName);

            foreach (var info in files)
            {
                var file = info as SlimFileInfo;
                if (file == null)
                {
                    this.LogWarning($"Not a file: {info.FullName}");
                    continue;
                }

                using (var stream = await fileOps.OpenFileAsync(file.FullName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
                {
                    this.LogDebug($"Uploading {file.Name} ({AH.FormatSize(file.Size)})");
                    await github.UploadReleaseAssetAsync(this.OrganizationName, this.RepositoryName, this.Tag, file.Name, this.ContentType, new PositionStream(stream, file.Size)).ConfigureAwait(false);
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.RepositoryName)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Upload ", new MaskHilite(this.Includes, this.Excludes), " from ", new DirectoryHilite(this.SourceDirectory), " to GitHub"),
               new RichDescription("in ", new Hilite(source), " release ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
