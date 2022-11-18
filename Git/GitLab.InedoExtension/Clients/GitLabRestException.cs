using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable  enable

namespace Inedo.Extensions.GitLab.Clients
{
    internal sealed class GitLabRestException : Exception
    {
        public GitLabRestException(int statusCode, string message, Exception? inner = null)
            : base(message, inner)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; }

        public static async Task ThrowIfErrorAsync(HttpResponseMessage response, string url, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new GitLabRestException((int)response.StatusCode, $"GitLab Error {(int)response.StatusCode}: verify that your GitLab credentials used to connect are correct.");
            else if (response.StatusCode == HttpStatusCode.NotFound)
                throw new GitLabRestException(404, $"GitLab Error {(int)response.StatusCode}: verify that the URL in the operation or credentials is correct (resolved to {url}).");

            string? message = null;
            try
            {
                message = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                var jdoc = JsonDocument.Parse(message);
                if (jdoc?.RootElement.TryGetProperty("message", out var messageProp) == true)
                {
                    if (messageProp.ValueKind == JsonValueKind.Array)
                        message = string.Join(", ", messageProp.EnumerateArray().Select(e => e.GetString()));
                    else if (messageProp.ValueKind == JsonValueKind.String)
                        message = messageProp.GetString();
                }
            }
            catch
            {
            }

            if (message != null)
                throw new GitLabRestException((int)response.StatusCode, $"GitLab Error {(int)response.StatusCode}: {message}");
            else
                throw new GitLabRestException((int)response.StatusCode, $"GitLab Error {(int)response.StatusCode} ({response.StatusCode})");
        }
    }
}
