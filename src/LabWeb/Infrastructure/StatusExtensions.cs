using System.ComponentModel.DataAnnotations;
using System.Reflection;
using LabWeb.Models;

namespace LabWeb.Infrastructure;

public static class StatusExtensions
{
    public static string DisplayName(this Enum value) =>
        value.GetType().GetMember(value.ToString()).FirstOrDefault()
             ?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();

    public static string BadgeClass(this PointStatus status) => status switch
    {
        PointStatus.Working => "text-bg-success",
        PointStatus.Limited => "text-bg-warning",
        PointStatus.Closed => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string ToLocalString(this DateTime utc, string format = "dd.MM.yyyy HH:mm") =>
        utc.ToKyiv().ToString(format);

    public static DateTime ToKyiv(this DateTime utc)
    {
        var tz = TryFindKyivZone();
        return tz is null ? utc.ToLocalTime() : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
    }

    private static TimeZoneInfo? _kyiv;
    private static TimeZoneInfo? TryFindKyivZone()
    {
        if (_kyiv is not null) return _kyiv;
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            try { return _kyiv = TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return null;
    }
}
