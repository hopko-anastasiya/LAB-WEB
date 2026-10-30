using System.Globalization;
using LabWeb.Data;
using LabWeb.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddControllersWithViews(options =>
{
    var m = options.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "Обов'язкове поле");
    m.SetMissingBindRequiredValueAccessor(_ => "Обов'язкове поле");
    m.SetAttemptedValueIsInvalidAccessor((value, _) => $"Некоректне значення «{value}»");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Некоректне значення «{value}»");
    m.SetValueIsInvalidAccessor(value => $"Некоректне значення «{value}»");
    m.SetValueMustBeANumberAccessor(_ => "Значення має бути числом");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "Значення має бути числом");
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<PointStatusService>();

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
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
