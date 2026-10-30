using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LabWeb.Tests.Infrastructure;

public partial class BrowserSession(ApiFactory factory)
{
    public HttpClient Client { get; } = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    public async Task<string> AntiforgeryToken(string url)
    {
        var html = await Client.GetStringAsync(url);
        var match = TokenRegex().Match(html);
        Assert.True(match.Success, $"На сторінці {url} немає antiforgery-токена");
        return match.Groups[1].Value;
    }

    public async Task<HttpResponseMessage> PostForm(string url, string formPage, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await AntiforgeryToken(formPage);
        return await Client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    public async Task SignIn(string email, string name = "Тестовий користувач")
    {
        var response = await PostForm("/Account/DevLogin", "/Account/Login",
            new() { ["email"] = email, ["name"] = name });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
