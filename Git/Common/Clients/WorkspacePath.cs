using System;
using System.IO;
using System.Text.RegularExpressions;
using Inedo.Agents;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.Clients
{
    public sealed class WorkspacePath
    {
        private static readonly Regex PathSanitizerRegex = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]", RegexOptions.Compiled);

        public WorkspacePath(string fullPath)
        {
            if (!PathEx.IsPathRooted(fullPath ?? ""))
                throw new InvalidOperationException("Workspace path is not absolute.");

            this.FullPath = fullPath;
        }

        public WorkspacePath(string rootWorkspaceDirectory, string relativePath)
        {
            if (string.IsNullOrEmpty(rootWorkspaceDirectory))
                throw new ArgumentNullException(nameof(rootWorkspaceDirectory));

            string sanitized = PathSanitizerRegex.Replace(relativePath ?? "", "_");

            if (string.IsNullOrEmpty(sanitized))
                throw new InvalidOperationException("Workspace path is invalid.");

            this.FullPath = PathEx.Combine(rootWorkspaceDirectory, sanitized);
        }

        public static WorkspacePath Resolve(IOperationExecutionContext context, string suggestedRelativePath, string overriddenPath)
        {
            if (!string.IsNullOrWhiteSpace(overriddenPath))
            {
                return new WorkspacePath(context.ResolvePath(overriddenPath));
            }
            else
            {
                string rootWorkspacePath = PathEx.Combine(context.Agent.GetService<IFileOperationsExecuter>().GetBaseWorkingDirectory(), "GitWorkspaces");

                suggestedRelativePath = suggestedRelativePath.Trim('/');
                if (suggestedRelativePath.Contains("/"))
                    suggestedRelativePath = suggestedRelativePath.Substring(suggestedRelativePath.LastIndexOf('/') + 1);

                return new WorkspacePath(rootWorkspacePath, suggestedRelativePath);
            }
        }

        public string FullPath { get; }

        public override string ToString() => this.FullPath;
    }
}
