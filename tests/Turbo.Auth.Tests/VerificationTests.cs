using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Controllers.Auth;

namespace Turbo.Auth.Tests;

public class VerificationTests
{
    [Test]
    public void Captcha_returns_a_self_contained_svg_matching_the_code()
    {
        var result = (OkObjectResult)new VerificationController().GetCaptcha().Result!;
        var response = JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(response.GetProperty("contentType").GetString(), Is.EqualTo("image/svg+xml"));
        var code = response.GetProperty("code").GetString()!;
        Assert.That(code, Does.Match("^[A-Za-z0-9]{6}$"));
        var image = XElement.Parse(Encoding.UTF8.GetString(response.GetProperty("img").GetBytesFromBase64()));
        XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.Multiple(() =>
        {
            Assert.That(image.Name, Is.EqualTo(svg + "svg"));
            Assert.That((int)image.Attribute("width")!, Is.EqualTo(200));
            Assert.That((int)image.Attribute("height")!, Is.EqualTo(50));
            Assert.That(string.Concat(image.Elements(svg + "text").Select(e => e.Value)), Is.EqualTo(code));
            Assert.That(image.Descendants().All(e => e.Name == svg + "text" || e.Name == svg + "rect" || e.Name == svg + "line"), Is.True);
            Assert.That(image.Descendants().Attributes().Any(a => a.Name.LocalName is "href" or "style" || a.Name.LocalName.StartsWith("on")), Is.False);
        });
    }
}
