using System.Globalization;
using System.Text;
using System.Xml.Linq;
using LabWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

public class SeoController(AppDbContext db) : Controller
{
    private string BaseUrl => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

    [HttpGet("robots.txt")]
    public IActionResult Robots() => Content(
        $"""
        User-agent: *
        Disallow: /Account/
        Disallow: /Community/
        Disallow: /Analytics/
        Disallow: /Board/
        Disallow: /hubs/
        Disallow: /api/

        Sitemap: {BaseUrl}/sitemap.xml
        """, "text/plain", Encoding.UTF8);

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var pages = new[]
        {
            Url.Action("Index", "Home"), Url.Action("Index", "Map"), Url.Action("Index", "Points")
        }.Select(path => new XElement(ns + "url", new XElement(ns + "loc", BaseUrl + path)));

        var points = await db.Points.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.UpdatedAt })
            .ToListAsync(ct);
        var pointPages = points.Select(p => new XElement(ns + "url",
            new XElement(ns + "loc", BaseUrl + Url.Action("Details", "Points", new { id = p.Id })),
            new XElement(ns + "lastmod", p.UpdatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));

        var xml = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(ns + "urlset", pages, pointPages));
        return Content(xml.Declaration + "\n" + xml, "application/xml", Encoding.UTF8);
    }
}
