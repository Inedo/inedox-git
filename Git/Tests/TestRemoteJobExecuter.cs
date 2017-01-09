using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;

namespace Tests
{
    internal sealed class TestRemoteJobExecuter : IRemoteJobExecuter
    {
        Task<object> IRemoteJobExecuter.ExecuteJobAsync(RemoteJob job, CancellationToken cancellationToken)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            return Task.Factory.StartNew(
                () =>
                {
                    using (var remoteJob = DuplicateJob(job))
                    {
                        //remoteJob.PostData = d => job.NotifyDataReceived(new MemoryStream(d, false));

                        var result = remoteJob.ExecuteAsync(cancellationToken).Result;
                        return DuplicateResponse(result, remoteJob, job);
                    }
                }
            );
        }

        private static RemoteJob DuplicateJob(RemoteJob job)
        {
            var stream = new MemoryStream();
            job.Serialize(stream);
            stream.Position = 0;

            var remoteJob = (RemoteJob)Activator.CreateInstance(job.GetType());
            remoteJob.Deserialize(stream);
            return remoteJob;
        }
        private static object DuplicateResponse(object result, RemoteJob remoteJob, RemoteJob localJob)
        {
            var stream = new MemoryStream();
            remoteJob.SerializeResponse(stream, result);
            stream.Position = 0;
            return localJob.DeserializeResponse(stream);
        }
    }
}
