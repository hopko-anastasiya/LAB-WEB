using System.ComponentModel;
using System.Text.Json;
using LabWeb.Auth;
using LabWeb.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LabWeb.Assistant;

[McpServerToolType]
public static class McpTools
{
    [McpServerTool(Name = "search_points", ReadOnly = true)]
    [Description("Повнотекстовий пошук пунктів незламності за назвою, адресою, містом, послугами чи оператором.")]
    public static Task<string> SearchPoints(
        PointTools tools,
        [Description("Пошуковий запит, щонайменше 2 символи")] string query,
        [Description("Статус: Working, Limited або Closed")] PointStatus? status = null,
        [Description("Скільки результатів повернути, до 20")] int limit = 5,
        CancellationToken ct = default) =>
        Run(() => tools.SearchAsync(query, status, limit, ct));

    [McpServerTool(Name = "find_nearby_points", ReadOnly = true)]
    [Description("Знаходить найближчі пункти до населеного пункту або координат, з фільтром за послугами. Спочатку йдуть пункти, що працюють без обмежень.")]
    public static Task<string> FindNearby(
        PointTools tools,
        [Description("Назва населеного пункту, напр. Харків")] string? settlement = null,
        [Description("Широта, якщо відомі координати")] double? latitude = null,
        [Description("Довгота, якщо відомі координати")] double? longitude = null,
        [Description("Потрібні послуги, напр. генератор, Starlink. Пункт має мати всі")] string[]? services = null,
        [Description("Не показувати пункти, що не працюють")] bool onlyOpen = true,
        [Description("Радіус пошуку в кілометрах")] double radiusKm = 30,
        [Description("Скільки результатів повернути, до 20")] int limit = 5,
        CancellationToken ct = default) =>
        Run(() => tools.FindNearbyAsync(settlement, latitude, longitude, services, onlyOpen, radiusKm, limit, ct));

    [McpServerTool(Name = "get_point", ReadOnly = true)]
    [Description("Повертає поточний стан і дані одного пункту за його номером.")]
    public static Task<string> GetPoint(PointTools tools, [Description("Номер пункту")] int pointId, CancellationToken ct = default) =>
        Run(() => tools.GetPointAsync(pointId, ct));

    [McpServerTool(Name = "get_point_reports", ReadOnly = true)]
    [Description("Повертає останні звіти про стан пункту, спочатку найновіші.")]
    public static Task<string> GetReports(
        PointTools tools,
        [Description("Номер пункту")] int pointId,
        [Description("Скільки звітів повернути, до 20")] int limit = 5,
        CancellationToken ct = default) =>
        Run(() => tools.GetReportsAsync(pointId, limit, ct));

    [McpServerTool(Name = "list_services", ReadOnly = true)]
    [Description("Повертає довідник послуг, які можуть надавати пункти.")]
    public static Task<string> ListServices(PointTools tools, CancellationToken ct = default) =>
        Run(() => tools.ListServicesAsync(ct));

    [McpServerTool(Name = "add_status_report", Destructive = false)]
    [Description("Додає звіт про стан пункту своєї громади і оновлює його статус. Потрібен заголовок Authorization: Bearer з API-токеном із профілю на сайті.")]
    public static Task<string> AddReport(
        PointTools tools,
        IHttpContextAccessor http,
        [Description("Номер пункту")] int pointId,
        [Description("Статус: Working, Limited або Closed")] PointStatus status,
        [Description("Чи є світло")] bool hasElectricity,
        [Description("Чи є зв'язок")] bool hasInternet,
        [Description("Чи є тепло")] bool hasHeating,
        [Description("Скільки людей зараз у пункті")] int occupancy,
        [Description("Коментар, до 1000 символів")] string? comment = null,
        CancellationToken ct = default) =>
        Run(() =>
        {
            var context = http.HttpContext;
            var user = context?.RequestServices.GetRequiredService<CurrentUser>();
            if (user?.User is null || context!.User.Identity?.AuthenticationType != ApiTokenHandler.SchemeName)
                throw new PointToolException("Потрібен API-токен: заголовок Authorization: Bearer <токен>.");
            return tools.AddReportAsync(user, pointId, status, hasElectricity, hasInternet, hasHeating, occupancy, comment, ct);
        });

    private static async Task<string> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return JsonSerializer.Serialize(await action(), PointTools.Json);
        }
        catch (PointToolException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}
