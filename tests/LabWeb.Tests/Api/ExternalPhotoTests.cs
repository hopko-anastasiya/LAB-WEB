using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class ExternalPhotoTests(ApiFactory factory)
{
    [Fact]
    public async Task Photo_from_blob_storage_keeps_its_address()
    {
        const string url = "https://storage.example.com/uploads/points/photo.png";
        var api = factory.CreateAuthorizedClient();
        var pointId = await api.CreatePoint(await api.CreateOperator());
        await using (var db = factory.CreateDbContext())
        {
            db.Points.Single(p => p.Id == pointId).PhotoPath = url;
            await db.SaveChangesAsync();
        }

        var json = await (await api.GetAsync($"/api/points/{pointId}")).ExpectJson(HttpStatusCode.OK);
        var html = await factory.CreateClient().GetStringAsync($"/punkt/{pointId}");

        Assert.Equal(url, json["photoUrl"]!.GetValue<string>());
        Assert.Contains($"property=\"og:image\" content=\"{url}\"", html);
        Assert.Contains($"src=\"{url}\"", html);
    }

    [Fact]
    public async Task Fake_image_is_rejected_by_content()
    {
        var api = factory.CreateAuthorizedClient();
        var pointId = await api.CreatePoint(await api.CreateOperator());
        var content = new ByteArrayContent("<script>alert(1)</script>"u8.ToArray());
        content.Headers.ContentType = new("image/png");

        var response = await api.PostAsync($"/api/points/{pointId}/photo", new MultipartFormDataContent { { content, "file", "photo.png" } });

        var json = await response.ExpectJson(HttpStatusCode.BadRequest);
        Assert.Contains("не схожий", json["errors"]!["file"]![0]!.GetValue<string>());
    }
}
