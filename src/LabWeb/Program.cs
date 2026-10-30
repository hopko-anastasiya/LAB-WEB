using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using LabWeb.Api;
using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Hubs;
using LabWeb.Search;
using LabWeb.Services;
using LabWeb.Telegram;
using Microsoft.AspNetCore.Localization;
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
builder.AddAppAuth();
builder.Services.AddHttpContextAccessor();
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<PlanFilter>()
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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<PointsHub>("/hubs/points");

app.Run();

public partial class Program { }
