using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.Clients;
using Inedo.IO;

namespace Inedo.Extensions.GitHub.Operations.Releases
{
    [DisplayName("Upload GitHub Release Assets")]
    [Description("Uploads files as attachments to a GitHub release.")]
    [Tag("source-control")]
    [ScriptAlias("Upload-ReleaseAssets")]
    [ScriptAlias("GitHub-Upload-Release-Assets", Obsolete = true)]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    public sealed class GitHubUploadReleaseAssetsOperation : GitHubOperationBase
    {
        [Required]
        [ScriptAlias("Tag")]
        [DisplayName("Tag")]
        [Description("The tag associated with the release. The release must already exist.")]
        public string Tag { get; set; }

        [Required]
        [ScriptAlias("Include")]
        [MaskingDescription]
        [PlaceholderText("* (top-level items)")]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("Directory")]
        public string SourceDirectory { get; set; }

        [ScriptAlias("ContentType")]
        [DisplayName("Content type")]
        [Description(@"The content type of the assets. For a list of acceptable types, see the IANA list of <a href=""https://www.iana.org/assignments/media-types/media-types.xhtml"">media types</a>.")]
        [Example("application/zip")]
        [DefaultValue("application/octet-stream")]
        public string ContentType { get; set; } = "application/octet-stream";

        private OperationProgress progress = null;
        public override OperationProgress GetProgress() => this.progress;

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.progress = null;

            var sourceDirectory = context.ResolvePath(this.SourceDirectory);

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var files = await fileOps.GetFileSystemInfosAsync(sourceDirectory, new MaskingContext(this.Includes, this.Excludes)).ConfigureAwait(false);
            if (files.Count == 0)
            {
                this.LogWarning("No files matched.");
                return;
            }

            var (credentials, resource) = this.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var github = new GitHubClient(credentials, resource);

            var ownerName = AH.CoalesceString(this.OrganizationName, this.UserName);

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
                    await github.UploadReleaseAssetAsync(ownerName, this.RepositoryName, this.Tag, file.Name, this.ContentType, new PositionStream(stream, file.Size), pos => this.progress = new OperationProgress((int)(100 * pos / file.Size), $"Uploading {file.Name} ({AH.FormatSize(pos)} / {AH.FormatSize(file.Size)})"), context.CancellationToken).ConfigureAwait(false);
                    this.progress = null;
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.RepositoryName)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Upload ", new MaskHilite(config[nameof(this.Includes)], config[nameof(this.Excludes)]), " from ", new DirectoryHilite(config[nameof(this.SourceDirectory)]), " to GitHub"),
               new RichDescription("in ", new Hilite(source), " release ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
