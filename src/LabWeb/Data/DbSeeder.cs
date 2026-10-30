using LabWeb.Auth;
using LabWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Services.AnyAsync()) return;

        var charging = new Service { Name = "Зарядка пристроїв", Icon = "bi-battery-charging", Description = "Розетки та павербанки для телефонів і ноутбуків" };
        var wifi = new Service { Name = "Wi-Fi / Starlink", Icon = "bi-wifi", Description = "Безкоштовний доступ до інтернету" };
        var heat = new Service { Name = "Обігрів", Icon = "bi-thermometer-sun", Description = "Тепле приміщення" };
        var water = new Service { Name = "Питна вода", Icon = "bi-droplet", Description = "Вода та гарячі напої" };
        var aid = new Service { Name = "Перша допомога", Icon = "bi-heart-pulse", Description = "Аптечка, черговий медик" };
        var generator = new Service { Name = "Генератор", Icon = "bi-lightning-charge", Description = "Автономне живлення" };

        var kyiv = new Community { Name = "Київська міська громада", Plan = Plan.Premium, InviteCode = UserAccounts.NewInviteCode() };
        var lviv = new Community { Name = "Львівська міська громада", Plan = Plan.Free, InviteCode = UserAccounts.NewInviteCode() };

        var op1 = new Operator { Community = kyiv, Name = "Олена Коваль", Organization = "ДСНС Київ", Phone = "+380671112233", Email = "koval@example.com", Telegram = "@olena_koval" };
        var op2 = new Operator { Community = kyiv, Name = "Андрій Мельник", Organization = "КП «Київбудсервіс»", Phone = "+380502223344", Email = "melnyk@example.com" };
        var op3 = new Operator { Community = lviv, Name = "Ірина Бондар", Organization = "Львівська міська рада", Phone = "+380933334455", Telegram = "@ibondar" };

        var now = DateTime.UtcNow;
        var points = new List<Point>
        {
            new()
            {
                Name = "Пункт незламності — Школа №52", Address = "вул. Хрещатик, 10", Settlement = "Київ",
                Latitude = 50.4485, Longitude = 30.5225, Capacity = 120, Schedule = "Цілодобово",
                Description = "Великий пункт у спортзалі школи. Є окрема зона для дітей.",
                Operator = op1, Services = [charging, wifi, heat, water, generator], Status = PointStatus.Working
            },
            new()
            {
                Name = "Пункт у ЦНАП Оболонь", Address = "просп. Оболонський, 16", Settlement = "Київ",
                Latitude = 50.5017, Longitude = 30.4983, Capacity = 60, Schedule = "08:00–22:00",
                Description = "Працює під час відключень світла.",
                Operator = op2, Services = [charging, wifi, water], Status = PointStatus.Limited
            },
            new()
            {
                Name = "Пункт ДСНС Поділ", Address = "вул. Сагайдачного, 25", Settlement = "Київ",
                Latitude = 50.4628, Longitude = 30.5196, Capacity = 40, Schedule = "Цілодобово",
                Operator = op1, Services = [charging, heat, aid, generator], Status = PointStatus.Working
            },
            new()
            {
                Name = "Пункт на площі Ринок", Address = "пл. Ринок, 1", Settlement = "Львів",
                Latitude = 49.8419, Longitude = 24.0315, Capacity = 80, Schedule = "09:00–21:00",
                Operator = op3, Services = [charging, wifi, heat, water], Status = PointStatus.Closed
            }
        };

        db.AddRange(points);

        db.StatusReports.AddRange(
            new StatusReport { Point = points[0], Status = PointStatus.Working, HasElectricity = true, HasInternet = true, HasHeating = true, Occupancy = 35, AuthorName = "Олена Коваль", Comment = "Все працює, генератор заправлений.", CreatedAt = now.AddHours(-3) },
            new StatusReport { Point = points[0], Status = PointStatus.Working, HasElectricity = true, HasInternet = true, HasHeating = true, Occupancy = 52, AuthorName = "Олена Коваль", CreatedAt = now.AddMinutes(-40) },
            new StatusReport { Point = points[1], Status = PointStatus.Limited, HasElectricity = false, HasInternet = true, HasHeating = false, Occupancy = 18, AuthorName = "Андрій Мельник", Comment = "Немає світла, працює лише Starlink від акумулятора.", CreatedAt = now.AddHours(-1) },
            new StatusReport { Point = points[2], Status = PointStatus.Working, HasElectricity = true, HasInternet = false, HasHeating = true, Occupancy = 12, AuthorName = "Олена Коваль", CreatedAt = now.AddHours(-2) },
            new StatusReport { Point = points[3], Status = PointStatus.Closed, HasElectricity = false, HasInternet = false, HasHeating = false, Occupancy = 0, AuthorName = "Ірина Бондар", Comment = "Тимчасово зачинено на технічне обслуговування.", CreatedAt = now.AddHours(-5) }
        );

        await db.SaveChangesAsync();
    }
}
