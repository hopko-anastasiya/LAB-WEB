using LabWeb.Auth;
using LabWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Data;

public static class DemoDataSeeder
{
    private const string Marker = "Харківська міська громада";

    private static readonly string[] BasePoints =
    [
        "Пункт незламності — Школа №52", "Пункт у ЦНАП Оболонь", "Пункт ДСНС Поділ", "Пункт на площі Ринок"
    ];

    private record DemoPoint(string Name, string Address, double Lat, double Lng, int Capacity, string Schedule, string? Description, string[] Services);

    private record DemoCity(string Settlement, string Community, Plan Plan, (string Name, string Org, string Phone, string? Telegram)[] Operators, DemoPoint[] Points);

    private const string Charging = "Зарядка пристроїв";
    private const string Wifi = "Wi-Fi / Starlink";
    private const string Heat = "Обігрів";
    private const string Water = "Питна вода";
    private const string Aid = "Перша допомога";
    private const string Generator = "Генератор";

    private static readonly DemoCity[] Cities =
    [
        new("Київ", "Київська міська громада", Plan.Premium,
            [("Тарас Литвиненко", "Дарницька РДА", "+380441234501", "@lytvynenko_t")],
            [
                new("Пункт у ліцеї №293", "вул. Срібнокільська, 14", 50.4012, 30.6153, 90, "Цілодобово", "Спортзал ліцею, окрема кімната для матерів із дітьми.", [Charging, Wifi, Heat, Water, Generator]),
                new("ЦНАП Дарниця", "просп. Петра Григоренка, 22", 50.4156, 30.6342, 50, "08:00–20:00", null, [Charging, Wifi, Water]),
                new("Пункт ДСНС Святошин", "просп. Перемоги, 97", 50.4573, 30.3907, 60, "Цілодобово", "Намет ДСНС біля станції метро.", [Charging, Heat, Aid, Generator]),
                new("Бібліотека на Троєщині", "вул. Лісківська, 4", 50.5101, 30.6040, 30, "09:00–21:00", "Тиха зона для роботи з ноутбуком.", [Charging, Wifi, Heat]),
                new("Палац культури «Дарниця»", "вул. Волго-Донська, 1", 50.4440, 30.6339, 120, "Цілодобово", null, [Charging, Wifi, Heat, Water, Aid, Generator]),
                new("Пункт у ТРЦ «Скай Молл»", "вул. Генерала Ватутіна, 2Т", 50.4924, 30.5973, 150, "10:00–22:00", "Фудкорт другого поверху, генератор торгового центру.", [Charging, Wifi, Water, Generator])
            ]),
        new("Львів", "Львівська міська громада", Plan.Free,
            [("Остап Гнатів", "Волонтерський штаб «Левиця»", "+380322345602", "@ostap_hn")],
            [
                new("Пункт у школі №84", "вул. Пасічна, 92", 49.8222, 24.0675, 70, "Цілодобово", null, [Charging, Wifi, Heat, Water]),
                new("Пункт на Сихові", "просп. Червоної Калини, 60", 49.7980, 24.0538, 80, "08:00–22:00", "Поруч зупинка трамвая №3.", [Charging, Wifi, Heat, Generator]),
                new("Центр «Левандівка»", "вул. Широка, 87", 49.8467, 23.9625, 40, "09:00–21:00", null, [Charging, Water, Aid])
            ]),
        new("Харків", Marker, Plan.Premium,
            [("Вікторія Шевчук", "ДСНС у Харківській області", "+380572345603", "@vika_shevchuk"), ("Дмитро Ковальов", "КП «Харківводоканал»", "+380577654321", null)],
            [
                new("Пункт у метро «Героїв Праці»", "вул. Гвардійців-Широнінців, 2", 50.0255, 36.3352, 300, "Цілодобово", "Станція метро, працює і як укриття.", [Charging, Wifi, Heat, Water, Aid]),
                new("Пункт ДСНС Салтівка", "просп. Тракторобудівників, 104", 50.0298, 36.3500, 60, "Цілодобово", null, [Charging, Heat, Aid, Generator]),
                new("Ліцей №17", "вул. Пушкінська, 46", 49.9990, 36.2423, 80, "08:00–21:00", null, [Charging, Wifi, Heat, Water]),
                new("Пункт у Палаці спорту", "просп. Героїв Харкова, 144", 49.9781, 36.2913, 200, "Цілодобово", "Велика зала, є місця для ночівлі.", [Charging, Wifi, Heat, Water, Aid, Generator])
            ]),
        new("Одеса", "Одеська міська громада", Plan.Premium,
            [("Ганна Петренко", "Одеська міська рада", "+380482345604", "@anna_petrenko_od")],
            [
                new("Пункт на Таїрова", "вул. Академіка Корольова, 46", 46.4060, 30.7184, 70, "Цілодобово", null, [Charging, Wifi, Heat, Water, Generator]),
                new("Пункт у морвокзалі", "вул. Приморська, 6", 46.4927, 30.7484, 120, "08:00–22:00", null, [Charging, Wifi, Water]),
                new("Школа №121 на Черемушках", "вул. Варненська, 25", 46.4385, 30.7118, 60, "Цілодобово", null, [Charging, Heat, Water, Aid])
            ]),
        new("Дніпро", "Дніпровська міська громада", Plan.Premium,
            [("Сергій Мороз", "КП «Дніпровська аварійна служба»", "+380562345605", "@moroz_dnipro")],
            [
                new("Пункт у ЦНАП Лівобережний", "просп. Слобожанський, 31Д", 48.5121, 35.0841, 60, "08:00–20:00", null, [Charging, Wifi, Water]),
                new("Пункт ДСНС на Перемозі", "вул. Набережна Перемоги, 86", 48.4205, 35.0715, 50, "Цілодобово", null, [Charging, Heat, Aid, Generator]),
                new("Палац студентів", "просп. Гагаріна, 59", 48.4334, 35.0518, 100, "09:00–22:00", "Працює коворкінг зі Starlink.", [Charging, Wifi, Heat, Generator])
            ]),
        new("Вінниця", "Вінницька міська громада", Plan.Free,
            [("Олег Бондаренко", "Вінницька міська рада", "+380432345606", null)],
            [
                new("Пункт на Вишеньці", "вул. Келецька, 78", 49.2244, 28.4120, 60, "Цілодобово", null, [Charging, Wifi, Heat, Water, Generator]),
                new("Пункт у центрі «Квадрат»", "вул. Соборна, 64", 49.2335, 28.4711, 40, "08:00–20:00", null, [Charging, Wifi, Water])
            ]),
        new("Біла Церква", "Білоцерківська міська громада", Plan.Free,
            [("Наталія Кравець", "Білоцерківська міська рада", "+380456345607", "@kravets_bc")],
            [
                new("Пункт у ліцеї №5", "вул. Леваневського, 53", 49.7826, 30.1567, 50, "Цілодобово", null, [Charging, Heat, Water, Generator]),
                new("Пункт на Гаї", "бульв. Олександрійський, 128", 49.8020, 30.1180, 40, "09:00–21:00", null, [Charging, Wifi, Heat])
            ]),
        new("Ірпінь", "Ірпінська міська громада", Plan.Free,
            [("Максим Ткач", "Ірпінська міська рада", "+380459345608", null)],
            [
                new("Пункт у Центрі культури", "вул. Шевченка, 2А", 50.5205, 30.2491, 80, "Цілодобово", null, [Charging, Wifi, Heat, Water, Generator]),
                new("Пункт ДСНС Ірпінь", "вул. Університетська, 31", 50.5130, 30.2330, 40, "Цілодобово", null, [Charging, Heat, Aid])
            ]),
        new("Чернігів", "Чернігівська міська громада", Plan.Free,
            [("Людмила Савченко", "ДСНС у Чернігівській області", "+380462345609", "@savchenko_ch")],
            [
                new("Пункт на Масанах", "вул. Шевченка, 158", 51.5163, 31.2994, 50, "Цілодобово", null, [Charging, Heat, Water, Aid, Generator]),
                new("Пункт у Драмтеатрі", "вул. Магістратська, 2", 51.4936, 31.3035, 70, "08:00–22:00", "Фойє театру, біля входу генератор.", [Charging, Wifi, Heat, Generator])
            ]),
        new("Запоріжжя", "Запорізька міська громада", Plan.Free,
            [("Андрій Сидоренко", "КП «Запоріжміськсвітло»", "+380612345610", null)],
            [
                new("Пункт на Бабурці", "вул. Шкільна, 9", 47.8641, 35.0412, 60, "Цілодобово", null, [Charging, Heat, Water, Generator]),
                new("Пункт у ЦНАП Вознесенівський", "просп. Соборний, 206", 47.8460, 35.1251, 50, "08:00–20:00", null, [Charging, Wifi, Water])
            ]),
        new("Полтава", "Полтавська міська громада", Plan.Free,
            [("Ірина Мельничук", "Полтавська міська рада", "+380532345611", "@melnychuk_pl")],
            [
                new("Пункт у ліцеї №1", "вул. Пушкіна, 77", 49.5902, 34.5532, 60, "Цілодобово", null, [Charging, Wifi, Heat, Water])
            ]),
        new("Житомир", "Житомирська міська громада", Plan.Free,
            [("Юрій Олійник", "Житомирська міська рада", "+380412345612", null)],
            [
                new("Пункт на Польовій", "вул. Польова, 2", 50.2614, 28.6762, 50, "Цілодобово", null, [Charging, Heat, Water, Generator]),
                new("Пункт у бібліотеці Ольжича", "вул. Велика Бердичівська, 22", 50.2511, 28.6614, 30, "09:00–20:00", null, [Charging, Wifi, Heat])
            ]),
        new("Чернівці", "Чернівецька міська громада", Plan.Free,
            [("Марта Гуцул", "Волонтерський центр «Буковина»", "+380372345613", "@marta_hutsul")],
            [
                new("Пункт на Садгорі", "вул. Ізмаїльська, 25", 48.3349, 25.9449, 40, "09:00–21:00", null, [Charging, Wifi, Water])
            ]),
        new("Миколаїв", "Миколаївська міська громада", Plan.Free,
            [("Віталій Гончар", "ДСНС у Миколаївській області", "+380512345614", null)],
            [
                new("Пункт у Намиві", "вул. Космонавтів, 81", 46.9578, 32.0430, 80, "Цілодобово", "Тут також видають технічну воду.", [Charging, Heat, Water, Aid, Generator]),
                new("Пункт на Соборній", "вул. Соборна, 12", 46.9695, 31.9971, 50, "08:00–21:00", null, [Charging, Wifi, Water])
            ])
    ];

    private static readonly string[] WorkingComments =
    [
        "Все працює, людей небагато.", "Генератор заправлений на добу.", "Starlink стабільний, розеток вистачає.",
        "Є гарячий чай і печиво.", "Тепло, можна залишитися на ніч.", "Приходьте з павербанками, зарядимо."
    ];

    private static readonly string[] LimitedComments =
    [
        "Немає світла, працюємо від акумуляторів.", "Інтернет з перебоями.", "Генератор на ремонті, лише зарядка телефонів.",
        "Багато людей, місць майже немає.", "Опалення ледь працює, беріть теплий одяг.", "Закінчується пальне для генератора."
    ];

    private static readonly string[] ClosedComments =
    [
        "Тимчасово зачинено: ремонт генератора.", "Зачинено через повітряну тривогу, перейдіть в укриття.",
        "Немає персоналу на зміні.", "Приміщення без світла й тепла, зачинено до ранку."
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Communities.AnyAsync(c => c.Name == Marker, ct)) return;

        var services = await db.Services.ToDictionaryAsync(s => s.Name, ct);
        if (services.Count == 0) return;

        var random = new Random(2026);
        var (from, to) = ReportPeriod();
        var points = new List<Point>();

        foreach (var city in Cities)
        {
            var community = await db.Communities.FirstOrDefaultAsync(c => c.Name == city.Community, ct)
                            ?? db.Communities.Add(new Community { Name = city.Community, Plan = city.Plan, InviteCode = UserAccounts.NewInviteCode() }).Entity;
            var operators = city.Operators
                .Select(o => new Operator { Community = community, Name = o.Name, Organization = o.Org, Phone = o.Phone, Telegram = o.Telegram })
                .ToList();
            db.Operators.AddRange(operators);

            for (var i = 0; i < city.Points.Length; i++)
            {
                var p = city.Points[i];
                var point = new Point
                {
                    Name = p.Name, Address = p.Address, Settlement = city.Settlement,
                    Latitude = p.Lat, Longitude = p.Lng, Capacity = p.Capacity, Schedule = p.Schedule,
                    Description = p.Description, Operator = operators[i % operators.Count],
                    Services = p.Services.Where(services.ContainsKey).Select(n => services[n]).ToList(),
                    CreatedAt = from.AddDays(-random.Next(5, 40))
                };
                db.Points.Add(point);
                points.Add(point);
            }
        }

        var basePoints = await db.Points.Include(p => p.Operator).Where(p => BasePoints.Contains(p.Name)).ToListAsync(ct);
        foreach (var point in basePoints)
            AddReports(db, point, random, from, to.AddHours(-6), updateStatus: false);
        foreach (var point in points)
            AddReports(db, point, random, from, to, updateStatus: true);

        await db.SaveChangesAsync(ct);
    }

    private static (DateTime From, DateTime To) ReportPeriod()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (now - monthStart).TotalDays >= 10 ? (monthStart, now) : (now.AddDays(-30), now);
    }

    private static void AddReports(AppDbContext db, Point point, Random random, DateTime from, DateTime to, bool updateStatus)
    {
        var count = random.Next(8, 15);
        var span = (to - from).TotalMinutes;
        var times = Enumerable.Range(0, count)
            .Select(_ => from.AddMinutes(random.NextDouble() * span))
            .Select(t => t.Date.AddHours(Math.Clamp(t.Hour, 5, 20)).AddMinutes(t.Minute))
            .Where(t => t <= to)
            .OrderBy(t => t)
            .ToList();

        var author = point.Operator?.Name;
        StatusReport? last = null;
        foreach (var time in times)
        {
            var status = NextStatus(random, last?.Status);
            var report = status switch
            {
                PointStatus.Working => new StatusReport
                {
                    HasElectricity = random.NextDouble() < .92, HasInternet = random.NextDouble() < .85, HasHeating = random.NextDouble() < .8,
                    Occupancy = (int)(point.Capacity * (.15 + random.NextDouble() * .6)),
                    Comment = random.NextDouble() < .45 ? Pick(random, WorkingComments) : null
                },
                PointStatus.Limited => new StatusReport
                {
                    HasElectricity = random.NextDouble() < .35, HasInternet = random.NextDouble() < .6, HasHeating = random.NextDouble() < .4,
                    Occupancy = (int)(point.Capacity * (.5 + random.NextDouble() * .5)),
                    Comment = random.NextDouble() < .75 ? Pick(random, LimitedComments) : null
                },
                _ => new StatusReport { Occupancy = 0, Comment = Pick(random, ClosedComments) }
            };
            report.Point = point;
            report.Status = status;
            report.AuthorName = author;
            report.CreatedAt = time;
            db.StatusReports.Add(report);
            last = report;
        }

        if (last is null || !updateStatus) return;
        point.Status = last.Status;
        point.UpdatedAt = last.CreatedAt;
    }

    private static PointStatus NextStatus(Random random, PointStatus? previous)
    {
        var roll = random.NextDouble();
        return previous switch
        {
            PointStatus.Closed => roll < .6 ? PointStatus.Working : roll < .85 ? PointStatus.Limited : PointStatus.Closed,
            PointStatus.Limited => roll < .55 ? PointStatus.Working : roll < .85 ? PointStatus.Limited : PointStatus.Closed,
            _ => roll < .7 ? PointStatus.Working : roll < .9 ? PointStatus.Limited : PointStatus.Closed
        };
    }

    private static string Pick(Random random, string[] items) => items[random.Next(items.Length)];
}
