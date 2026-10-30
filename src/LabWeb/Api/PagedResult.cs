namespace LabWeb.Api;

public record PagedResult<T>(IReadOnlyList<T> Items, int Skip, int Limit, int Total, string? NextLink);
