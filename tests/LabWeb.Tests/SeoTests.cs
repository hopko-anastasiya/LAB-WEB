using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests;

[Collection(ApiCollection.Name)]
public class SeoTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string Attr(string html, string pattern) => Regex.Match(html, pattern).Groups[1].Value;

    private async Task<int> CreatePoint(int[]? services = null)
    {
        var api = factory.CreateAuthorizedClient();
        return await api.CreatePoint(await api.CreateOperator(), serviceIds: services, settlement: "Овруч");
    }

    [Fact]
    public async Task Point_page_has_friendly_url_and_search_metadata()
    {
        var id = await CreatePoint();

        var html = await _client.GetStringAsync($"/punkt/{id}");

        Assert.Equal(1, Regex.Count(html, "<h1[ >]"));
        Assert.Contains("<title>Тестовий пункт, Овруч: пункт незламності — Незламні</title>", html);
        Assert.EndsWith($"/punkt/{id}", Attr(html, "<link rel=\"canonical\" href=\"([^\"]+)\""));
        var description = WebUtility.HtmlDecode(Attr(html, "<meta name=\"description\" content=\"([^\"]+)\""));
        Assert.Contains("Овруч", description);
        Assert.InRange(description.Length, 20, 160);
        Assert.Contains("property=\"og:title\"", html);
        Assert.DoesNotContain("noindex", html);
    }

    [Fact]
    public async Task Point_page_has_valid_structured_data()
    {
        var id = await CreatePoint();
        var html = await _client.GetStringAsync($"/punkt/{id}");

        var json = Attr(html, "<script type=\"application/ld\\+json\">(.+?)</script>");
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("CivicStructure", doc.RootElement.GetProperty("@type").GetString());
        Assert.Equal("Овруч", doc.RootElement.GetProperty("address").GetProperty("addressLocality").GetString());
        Assert.Equal(50.45, doc.RootElement.GetProperty("geo").GetProperty("latitude").GetDouble());
    }

    [Fact]
    public async Task Point_page_keeps_contacts_on_screen()
    {
        var id = await CreatePoint();
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync($"/punkt/{id}"));

        Assert.Contains("point-contact fixed-bottom", html);
        Assert.Contains("href=\"tel:+380501234567\"", html);
        Assert.Contains("google.com/maps/dir", html);
    }

    [Fact]
    public async Task Old_point_url_still_opens_with_same_canonical()
    {
        var id = await CreatePoint();
        var html = await _client.GetStringAsync($"/Points/Details/{id}");
        Assert.EndsWith($"/punkt/{id}", Attr(html, "<link rel=\"canonical\" href=\"([^\"]+)\""));
    }

    [Fact]
    public async Task Sitemap_lists_public_pages_and_points()
    {
        var id = await CreatePoint();

        var response = await _client.GetAsync("/sitemap.xml");
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var urls = xml.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value).ToList();

        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains(urls, u => u.EndsWith($"/punkt/{id}"));
        Assert.Contains(urls, u => u.EndsWith("/Map"));
        Assert.DoesNotContain(urls, u => u.Contains("/Account"));
    }

    [Fact]
    public async Task Robots_points_to_sitemap_and_hides_private_pages()
    {
        var robots = await _client.GetStringAsync("/robots.txt");

        Assert.Contains("/sitemap.xml", robots);
        Assert.Contains("Disallow: /Account/", robots);
        Assert.DoesNotContain("Disallow: /punkt", robots);
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Points/Create")]
    public async Task Service_pages_are_not_indexed(string url)
    {
        var browser = new BrowserSession(factory);
        await browser.SignIn(factory.CreateUser(communityName: "SEO громада").Email);
        var client = url.StartsWith("/Account") ? _client : browser.Client;

        Assert.Contains("noindex", await client.GetStringAsync(url));
    }

    [Fact]
    public async Task Home_page_has_description()
    {
        var html = await _client.GetStringAsync("/");
        Assert.NotEmpty(Attr(html, "<meta name=\"description\" content=\"([^\"]+)\""));
    }
}
