using System.Net;
using LabWeb.Messaging;
using LabWeb.Models;
using LabWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class NotificationTests(ApiFactory factory)
{
    private readonly HttpClient _api = factory.CreateAuthorizedClient();

    private async Task<int> NewPoint() => await _api.CreatePoint(await _api.CreateOperator());

    private async Task<(BrowserSession Browser, TestUser User)> Subscriber(int pointId)
    {
        var user = factory.CreateUser();
        var browser = new BrowserSession(factory);
        await browser.SignIn(user.Email);
        var response = await browser.PostForm("/Notifications/Subscribe", $"/punkt/{pointId}", new() { ["pointId"] = pointId.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return (browser, user);
    }

    private async Task<List<Notification>> WaitForNotifications(int userId, int count)
    {
        for (var i = 0; i < 100; i++)
        {
            await using var db = factory.CreateDbContext();
            var items = await db.Notifications.Where(n => n.UserId == userId).ToListAsync();
            if (items.Count >= count) return items;
            await Task.Delay(100);
        }
        throw new TimeoutException("Сповіщення не надійшло");
    }

    private Task Report(int pointId, string status) => _api.PostJson("/api/reports", new { pointId, status });

    [Fact]
    public async Task Status_change_reaches_subscriber_through_the_queue()
    {
        var pointId = await NewPoint();
        var (browser, user) = await Subscriber(pointId);

        await Report(pointId, "Closed");

        var item = Assert.Single(await WaitForNotifications(user.Id, 1));
        Assert.Equal(PointStatus.Working, item.OldStatus);
        Assert.Equal(PointStatus.Closed, item.NewStatus);
        var page = await browser.Client.GetStringAsync("/Notifications");
        Assert.Contains("Тестовий пункт", page);
        Assert.Contains("nav-badge", await browser.Client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Same_status_report_does_not_notify()
    {
        var pointId = await NewPoint();
        var (_, user) = await Subscriber(pointId);

        await Report(pointId, "Working");
        await Report(pointId, "Limited");

        var items = await WaitForNotifications(user.Id, 1);
        await Task.Delay(300);
        await using var db = factory.CreateDbContext();
        Assert.Equal(PointStatus.Limited, Assert.Single(db.Notifications.Where(n => n.UserId == user.Id)).NewStatus);
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task Repeated_message_creates_one_notification()
    {
        var pointId = await NewPoint();
        var (_, user) = await Subscriber(pointId);
        var message = new PointStatusChanged(Guid.NewGuid(), pointId, "Пункт", PointStatus.Working, PointStatus.Closed, DateTime.UtcNow);

        using var scope = factory.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<NotificationWriter>();
        await writer.WriteAsync(message);
        await writer.WriteAsync(message);

        await using var db = factory.CreateDbContext();
        Assert.Equal(1, db.Notifications.Count(n => n.UserId == user.Id));
    }

    [Fact]
    public async Task Unsubscribed_user_gets_nothing_and_notifications_are_private()
    {
        var pointId = await NewPoint();
        var (leaving, gone) = await Subscriber(pointId);
        var (_, stays) = await Subscriber(pointId);
        await leaving.PostForm("/Notifications/Unsubscribe", $"/punkt/{pointId}", new() { ["pointId"] = pointId.ToString() });

        await Report(pointId, "Closed");
        await WaitForNotifications(stays.Id, 1);

        await using var db = factory.CreateDbContext();
        Assert.False(db.Notifications.Any(n => n.UserId == gone.Id));
        Assert.DoesNotContain("Тестовий пункт", await leaving.Client.GetStringAsync("/Notifications"));
    }

    [Fact]
    public async Task Mark_read_clears_badge()
    {
        var pointId = await NewPoint();
        var (browser, user) = await Subscriber(pointId);
        await Report(pointId, "Closed");
        await WaitForNotifications(user.Id, 1);

        await browser.PostForm("/Notifications/MarkRead", "/Notifications", new());

        Assert.DoesNotContain("nav-badge", await browser.Client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Anonymous_visitor_is_sent_to_login()
    {
        var response = await new BrowserSession(factory).Client.GetAsync("/Notifications");
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery);
    }
}
