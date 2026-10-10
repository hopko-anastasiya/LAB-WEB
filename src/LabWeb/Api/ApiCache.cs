using LabWeb.Services;
using Microsoft.AspNetCore.OutputCaching;

namespace LabWeb.Api;

public static class ApiCache
{
    public const string Data = "api-data";
    public const string Reference = "api-reference";
    public const string DataTag = "data";

    public static void AddPolicies(OutputCacheOptions options)
    {
        options.AddPolicy(Data, b => b.Expire(TimeSpan.FromMinutes(5)).SetVaryByQuery("*").Tag(DataTag));
        options.AddPolicy(Reference, b => b.Expire(TimeSpan.FromHours(6)).SetVaryByQuery("*"));
    }
}

public class ApiCacheInvalidator(IOutputCacheStore store) : IDataChangeHandler
{
    public Task HandleAsync(DataChange change, CancellationToken ct = default) =>
        store.EvictByTagAsync(ApiCache.DataTag, ct).AsTask();
}
