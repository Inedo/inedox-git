namespace Inedo.Extensions.AzureDevOps
{
    internal static class InternalExtensions
    {
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var list = new List<T>();
            await foreach (var i in source.ConfigureAwait(false))
                list.Add(i);

            return list;
        }
    }
}
