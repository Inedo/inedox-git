using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using Inedo;
using Inedo.Agents;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Clients.CommandLine;
using Inedo.Extensions.Clients.LibGitSharp;
using Inedo.Extensions.Clients.LibGitSharp.Remote;
using Inedo.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class GitTests
    {
        private string repoUrl = "https://github.com/Inedo/git-test.git";
        private string credentialsFilePath = @"c:\tmp\.testcredentials";
        private string gitExePath = @"C:\Program Files\Git\cmd\git.exe";

        private string rootDir;
        private IFileOperationsExecuter fileOps;
        private IRemoteProcessExecuter processExecuter;
        private IRemoteJobExecuter jobExecuter;

        private string userName;
        private SecureString password;

        [TestInitialize]
        public void Initialize()
        {
            string asmDir = PathEx.GetDirectoryName(typeof(GitTests).Assembly.Location);
            this.rootDir = PathEx.Combine(asmDir, "test-root");
            DirectoryEx.Create(this.rootDir);
            DirectoryEx.Clear(this.rootDir);

            if (FileEx.Exists(credentialsFilePath))
            {
                var lines = File.ReadAllLines(credentialsFilePath);
                this.userName = lines[0];
                this.password = SecureStringExtensions.ToSecureString(lines[1]);
            }

            var fileOps = new TestFileOperationsExecuter(Path.Combine(this.rootDir, "agent"));

            //var fileOps = new SimulatedFileOperationsExecuter(fileOps);
            //fileOps.MessageLogged += (s, e) => TestLogger.Instance.Log(e.Level, e.Message);

            this.fileOps = fileOps;

            this.processExecuter = new TestRemoteProcessExecuter();
            this.jobExecuter = new TestRemoteJobExecuter();
        }

        [TestMethod]
        public void LibGitSharp_Clone()
        {
            this.Clone("clone-libgit", ClientType.LibGitSharp);
        }

        [TestMethod]
        public void LibGitSharp_Archive()
        {
            this.Archive("clone-libgit", "archive-libgit", ClientType.LibGitSharp);
        }

        [TestMethod]
        public void LibGitSharp_Tag()
        {
            this.Tag("clone-libgit", ClientType.LibGitSharp);
        }

        [TestMethod]
        public void LibGitSharp_Branches()
        {
            this.Branches("work-libgit", ClientType.LibGitSharp);
        }

        [TestMethod]
        public void RemoteLibGitSharp_Clone()
        {
            this.Clone("clone-remote-libgit", ClientType.RemoteLibGitSharp);
        }

        [TestMethod]
        public void RemoteLibGitSharp_Archive()
        {
            this.Archive("clone-remote-libgit", "archive-libgit", ClientType.RemoteLibGitSharp);
        }

        [TestMethod]
        public void RemoteLibGitSharp_Tag()
        {
            this.Tag("clone-remote-libgit", ClientType.RemoteLibGitSharp);
        }

        [TestMethod]
        public void RemoteLibGitSharp_Branches()
        {
            this.Branches("work-remote-libgit", ClientType.RemoteLibGitSharp);
        }

        [TestMethod]
        public void CommandLine_Clone()
        {
            this.Clone("clone-cmd", ClientType.CommandLine);
        }

        [TestMethod]
        public void CommandLine_Archive()
        {
            this.Archive("clone-cmd", "archive-cmd", ClientType.CommandLine);
        }

        [TestMethod]
        public void CommandLine_Tag()
        {
            this.Tag("clone-cmd", ClientType.CommandLine);
        }

        [TestMethod]
        public void CommandLine_Branches()
        {
            this.Branches("work-cmd", ClientType.CommandLine);
        }

        private void Clone(string cloneDirectory, ClientType type)
        {
            string fullCloneDirectory = PathEx.Combine(this.rootDir, cloneDirectory);

            var client = this.CreateClient(type, fullCloneDirectory);

            var options = new GitCloneOptions();

            client.CloneAsync(options).GetAwaiter().GetResult();

            string examplePath = PathEx.Combine(fullCloneDirectory, @"TestConsoleApplication\TestConsoleApplication\Resources\output.txt");
            Assert.IsTrue(this.fileOps.FileExists(examplePath));
        }

        private void Archive(string cloneDirectory, string archiveDirectory, ClientType type)
        {
            string fullCloneDirectory = PathEx.Combine(this.rootDir, cloneDirectory);
            string fullArchiveDirectory = PathEx.Combine(this.rootDir, archiveDirectory);

            var client = this.CreateClient(type, fullCloneDirectory);
            var options = new GitCloneOptions();

            client.CloneAsync(options).GetAwaiter().GetResult();

            client.ArchiveAsync(fullArchiveDirectory).GetAwaiter().GetResult();

            string examplePath = PathEx.Combine(fullArchiveDirectory, @"TestConsoleApplication\TestConsoleApplication\Resources\output.txt");
            Assert.IsTrue(this.fileOps.FileExists(examplePath));

            string gitDirectory = PathEx.Combine(fullArchiveDirectory, ".git");
            Assert.IsFalse(this.fileOps.DirectoryExists(gitDirectory));
        }

        private void Tag(string cloneDirectory, ClientType type)
        {
            string fullCloneDirectory = PathEx.Combine(this.rootDir, cloneDirectory);
            string tag = "tag-" + DateTime.Now.ToString("yyMMddhhmmss");

            var client = this.CreateClient(type, fullCloneDirectory);

            var options = new GitCloneOptions();
            client.CloneAsync(options).GetAwaiter().GetResult();

            client.TagAsync(tag).GetAwaiter().GetResult();
        }

        private void Branches(string workingDirectory, ClientType type)
        {
            string fullWorkingDirectory = PathEx.Combine(this.rootDir, workingDirectory);
            var client = this.CreateClient(type, fullWorkingDirectory);

            var branches = client.EnumerateRemoteBranchesAsync().GetAwaiter().GetResult().ToList();

            CollectionAssert.Contains(branches, "master");
            CollectionAssert.Contains(branches, "branch1");
        }

        private GitClient CreateClient(ClientType type, string workingDirectory)
        {
            var repo = new GitRepositoryInfo(new WorkspacePath(workingDirectory), repoUrl, this.userName, this.password);

            if (type == ClientType.CommandLine)
                return new GitCommandLineClient(gitExePath, this.processExecuter, this.fileOps, repo, TestLogger.Instance, CancellationToken.None);
            else if (type == ClientType.LibGitSharp)
                return new LibGitSharpClient(repo, TestLogger.Instance);
            else
                return new RemoteLibGitSharpClient(this.jobExecuter, workingDirectory, false, CancellationToken.None, repo, TestLogger.Instance);
        }

        private enum ClientType { CommandLine, LibGitSharp, RemoteLibGitSharp }
    }
}
