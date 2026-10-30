using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Api;

public static class PagingExtensions
{
    public static async Task<PagedResult<TDto>> ToPagedResultAsync<TEntity, TDto>(
        this IQueryable<TEntity> orderedQuery,
        PageQuery page,
        HttpRequest request,
        Func<TEntity, TDto> map,
        CancellationToken ct = default)
    {
        var total = await orderedQuery.CountAsync(ct);
        var items = await orderedQuery.Skip(page.Skip).Take(page.Limit).ToListAsync(ct);

        var nextSkip = page.Skip + items.Count;
        var nextLink = items.Count > 0 && nextSkip < total
            ? BuildLink(request, nextSkip, page.Limit)
            : null;

        return new PagedResult<TDto>(items.Select(map).ToList(), page.Skip, page.Limit, total, nextLink);
    }

    private static string BuildLink(HttpRequest request, int skip, int limit)
    {
        var query = request.Query
            .Where(q => !q.Key.Equals("skip", StringComparison.OrdinalIgnoreCase)
                     && !q.Key.Equals("limit", StringComparison.OrdinalIgnoreCase))
            .Append(new("skip", skip.ToString()))
            .Append(new("limit", limit.ToString()));

        var builder = new QueryBuilder(query.SelectMany(q => q.Value, (q, v) => new KeyValuePair<string, string>(q.Key, v ?? "")));
        return UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path, builder.ToQueryString());
    }
}
