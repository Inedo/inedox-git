using System.Collections.Generic;

namespace Inedo.Extensions.GitHub.Clients
{
    internal sealed record ProjectColumnData(string Name, List<string> IssueUrls);
}
