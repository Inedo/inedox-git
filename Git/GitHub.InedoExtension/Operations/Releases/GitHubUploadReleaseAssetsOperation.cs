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
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Operations.Releases
{
    [DisplayName("Upload GitHub Release Assets")]
    [Description("Uploads files as attachments to a GitHub release.")]
    [Tag("source-control"), Tag("git"), Tag("github")]
    [ScriptAlias("Upload-ReleaseAssets")]
    [ScriptAlias("GitHub-Upload-Release-Assets", Obsolete = true)]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    public sealed class GitHubUploadReleaseAssetsOperation : GitHubOperationBase
    {
        private readonly object progressLock = new();
        private SlimFileInfo currentFile;
        private long currentPosition;

        [Required]
        [ScriptAlias("Tag")]
        [Description("The tag associated with the release. The release must already exist.")]
        public string Tag { get; set; }

        [ScriptAlias("Include")]
        [Category("File Masks")]
        [DisplayName("Include files")]
        [MaskingDescription]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
        [Category("File Masks")]
        [DisplayName("Exclude files")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("Directory")]
        [DisplayName("From directory")]
        [PlaceholderText("$WorkingDirectory")]
        public string SourceDirectory { get; set; }

        [Category("Advanced")]
        [ScriptAlias("ContentType")]
        [DisplayName("Content type")]
        [PlaceholderText("detect from file extension")]
        public string ContentType { get; set; }

        public override OperationProgress GetProgress()
        {
            SlimFileInfo file;
            long pos;

            lock (this.progressLock)
            {
                if (this.currentFile == null)
                    return null;

                file = this.currentFile;
                pos = this.currentPosition;
            }

            return new OperationProgress((int)(100 * pos / file.Size), $"Uploading {file.Name} ({AH.FormatSize(pos)} / {AH.FormatSize(file.Size)})");
        }

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

            var (credentials, resource) = this.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var github = new GitHubClient(credentials, resource, this);

            var ownerName = AH.CoalesceString(resource.OrganizationName, credentials.UserName);

            foreach (var info in files)
            {
                if (info is not SlimFileInfo file)
                {
                    this.LogWarning($"Not a file: {info.FullName}");
                    continue;
                }

                lock (this.progressLock)
                {
                    this.currentFile = file;
                    this.currentPosition = 0;
                }

                using var stream = await fileOps.OpenFileAsync(file.FullName, FileMode.Open, FileAccess.Read).ConfigureAwait(false);

                var contentType = (string.IsNullOrWhiteSpace(this.ContentType) ? MimeMapping.GetMimeMapping(file.Name) : null) ?? "application/octet-stream";

                this.LogDebug($"Uploading {file.FullName} as {contentType} ({AH.FormatSize(file.Size)})...");
                await github.UploadReleaseAssetAsync(
                    ownerName,
                    resource.RepositoryName,
                    this.Tag,
                    file.Name,
                    contentType,
                    stream,
                    this.ReportProgress,
                    context.CancellationToken
                ).ConfigureAwait(false);

                this.LogInformation($"{file.FullName} uploaded.");
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Upload ", new MaskHilite(config[nameof(this.Includes)], config[nameof(this.Excludes)]), " from ", new DirectoryHilite(config[nameof(this.SourceDirectory)]), " to GitHub"),
               new RichDescription("in ", new Hilite(config.DescribeSource()), " release ", new Hilite(config[nameof(this.Tag)]))
            );
        }

        private void ReportProgress(long pos)
        {
            lock (this.progressLock)
            {
                this.currentPosition = pos;
            }
        }
    }
}
