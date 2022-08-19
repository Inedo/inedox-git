using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.Clients
{
    internal sealed class GitLabRestException : Exception
    {
        public GitLabRestException(int statusCode, string message, Exception inner = null)
            : base(message, inner)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; }

        public string FullMessage => $"The server returned an error ({this.StatusCode}): {this.Message}";

        public static async Task ThrowIfErrorAsync(HttpResponseMessage response, string url, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            try
            {
                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var jdoc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                var obj = jdoc.RootElement;
                if (obj.TryGetProperty("message", out var message))
                    throw new GitLabRestException((int)response.StatusCode, message.GetString());
            }
            catch
            {
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new GitLabRestException((int)response.StatusCode, "Verify that the credentials used to connect are correct.");
                else if(response.StatusCode == HttpStatusCode.NotFound)
                    throw new GitLabRestException(404, $"Verify that the URL in the operation or credentials is correct (resolved to {url}).");

                response.EnsureSuccessStatusCode();
            }

            throw new GitLabRestException((int)response.StatusCode, null);
        }
    }
}
