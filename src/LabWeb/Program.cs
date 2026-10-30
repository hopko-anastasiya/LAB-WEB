using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json.Serialization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using LabWeb.Api;
using LabWeb.Assistant;
using LabWeb.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Hubs;
using LabWeb.Messaging;
using LabWeb.Search;
using LabWeb.Services;
using LabWeb.Telegram;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.WebEncoders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddControllersWithViews(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    var m = options.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "Обов'язкове поле");
    m.SetMissingBindRequiredValueAccessor(_ => "Обов'язкове поле");
    m.SetAttemptedValueIsInvalidAccessor((value, _) => $"Некоректне значення «{value}»");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Некоректне значення «{value}»");
    m.SetValueIsInvalidAccessor(value => $"Некоректне значення «{value}»");
    m.SetValueMustBeANumberAccessor(_ => "Значення має бути числом");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "Значення має бути числом");
})
.AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
})
.ConfigureApiBehaviorOptions(o =>
{
    var defaultFactory = o.InvalidModelStateResponseFactory;
    o.InvalidModelStateResponseFactory = context =>
    {
        var result = defaultFactory(context);
        if (result is ObjectResult { Value: ValidationProblemDetails problem })
        {
            problem.Title = "Дані запиту некоректні";
            foreach (var key in problem.Errors.Keys.Where(k => k.StartsWith("$")).ToList())
            {
                problem.Errors.Remove(key);
                var field = key.TrimStart('$', '.');
                problem.Errors[field.Length > 0 ? field : "body"] = ["Некоректне значення або формат JSON"];
            }
        }
        return result;
    };
});

builder.Services.AddSingleton<DataChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
            npgsql => npgsql.EnableRetryOnFailure())
        .AddInterceptors(sp.GetRequiredService<DataChangesInterceptor>()));

builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<PointStatusService>();
builder.Services.AddScoped<IDataChangeHandler, PointsBroadcaster>();
builder.Services.AddSingleton<PointSearchIndex>();
builder.Services.AddScoped<IDataChangeHandler, SearchIndexUpdater>();
builder.Services.AddScoped<IDataChangeHandler, ApiCacheInvalidator>();
builder.Services.AddOutputCache(ApiCache.AddPolicies);
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.AddAppAuth();
var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
if (string.IsNullOrWhiteSpace(rabbit.Host))
    builder.Services.AddSingleton<IStatusEventQueue, InMemoryStatusEventQueue>();
else
    builder.Services.AddSingleton<IStatusEventQueue>(sp => new RabbitMqStatusEventQueue(rabbit, sp.GetRequiredService<ILogger<RabbitMqStatusEventQueue>>()));
builder.Services.AddScoped<IDataChangeHandler, StatusChangePublisher>();
builder.Services.AddScoped<NotificationWriter>();
builder.Services.AddHostedService<NotificationConsumerService>();
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Ai:Gemini"));
builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(c => c.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddScoped<PointTools>();
builder.Services.AddScoped<AssistantAgent>();
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AssistantController.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(10) }));
});
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).SetApplicationName("LabWeb");
builder.Services.AddHttpContextAccessor();
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<PlanFilter>()
    .AddFeatureFilter<ExplicitPlanFilter>()
    .UseDisabledFeaturesHandler(new RedirectToPremium());
builder.Services.AddHostedService<SearchIndexInitializer>();
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddHttpClient<ITelegramApi, TelegramApi>(c => c.Timeout = TimeSpan.FromSeconds(40));
builder.Services.AddSingleton<BotSessions>();
builder.Services.AddScoped<ReportBot>();
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddSignalR().AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.PayloadSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

var app = builder.Build();

var culture = (CultureInfo)CultureInfo.GetCultureInfo("uk-UA").Clone();
culture.NumberFormat.NumberDecimalSeparator = ".";
culture.NumberFormat.CurrencyDecimalSeparator = ".";
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseMigrator.MigrateAsync(db);
    await DbSeeder.SeedAsync(db);
    await DemoDataSeeder.SeedAsync(db);
    await SettlementImporter.ImportAsync(db, app.Environment.ContentRootPath);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseOutputCache();
app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "point",
    pattern: "punkt/{id:int}",
    defaults: new { controller = "Points", action = "Details" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<PointsHub>("/hubs/points");
app.MapHub<BoardHub>("/hubs/board");
app.MapMcp("/mcp");

app.Run();

public partial class Program { }
