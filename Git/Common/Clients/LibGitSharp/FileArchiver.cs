using System;
using System.IO;
using Inedo.Agents;
using Inedo.IO;
using LibGit2Sharp;

namespace Inedo.Extensions.Clients.LibGitSharp
{
    internal sealed class FileArchiver : ArchiverBase
    {
        private string rootPath;

        public FileArchiver(string rootPath)
        {
            if (rootPath == null)
                throw new ArgumentNullException(nameof(rootPath));

            this.rootPath = rootPath;
        }

        protected override void AddTreeEntry(string path, TreeEntry entry, DateTimeOffset modificationTime)
        {
            switch (entry.Mode)
            {
                case Mode.Directory:
                    CreateDirectory(path);
                    break;
                case Mode.NonExecutableFile:
                case Mode.NonExecutableGroupWritableFile:
                case Mode.ExecutableFile:
                    CreateFile(path, (Blob)entry.Target);
                    break;
                case Mode.SymbolicLink:
                case Mode.GitLink:
                case Mode.Nonexistent:
                default:
                    break;
            }
        }

        private void CreateFile(string relativePath, Blob target)
        {
            string path = PathEx.Combine(this.rootPath, relativePath);
            string directory = PathEx.GetDirectoryName(path);

            DirectoryEx.Create(directory);

            using (var targetStream = target.GetContentStream())
            using (var file = FileEx.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                targetStream.CopyTo(file);
            }
        }

        private void CreateDirectory(string relativePath)
        {
            string path = PathEx.Combine(this.rootPath, relativePath);
            DirectoryEx.Create(path);
        }
    }
}
