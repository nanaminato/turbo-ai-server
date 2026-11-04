using Turbo.Auth.Contracts.Auth;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Turbo.Auth.Controllers.Auth;

[ApiController]
[Route("api/verification")]
public class VerificationController : ControllerBase
{
    [HttpGet("generate")]
    public ActionResult<CaptchaResponse> GetCaptcha()
    {
        const string alphabet = "ABCDEFGHIJKMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var code = new string(Enumerable.Range(0, 6)
            .Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
        return Ok(new CaptchaResponse(GenerateCaptchaImage(code), code, "image/svg+xml"));
    }

    [HttpGet("check-token")]
    [Authorize]
    public IActionResult CheckToken() => Ok();

    // SVG uses client fonts and XML escaping, with no native graphics or font installation requirement.
    private static byte[] GenerateCaptchaImage(string code)
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        var image = new XElement(svg + "svg", new XAttribute("width", 200),
            new XAttribute("height", 50), new XAttribute("viewBox", "0 0 200 50"),
            new XElement(svg + "rect", new XAttribute("width", 200),
                new XAttribute("height", 50), new XAttribute("fill", "#18243a")));
        for (var i = 0; i < code.Length; i++)
        {
            image.Add(new XElement(svg + "text", new XAttribute("x", 18 + i * 27),
                new XAttribute("y", RandomNumberGenerator.GetInt32(30, 39)),
                new XAttribute("font-family", "monospace"), new XAttribute("font-size", 26),
                new XAttribute("fill", "#ffffff"), code[i]));
        }
        for (var i = 0; i < 5; i++)
        {
            image.Add(new XElement(svg + "line",
                new XAttribute("x1", RandomNumberGenerator.GetInt32(200)),
                new XAttribute("y1", RandomNumberGenerator.GetInt32(50)),
                new XAttribute("x2", RandomNumberGenerator.GetInt32(200)),
                new XAttribute("y2", RandomNumberGenerator.GetInt32(50)),
                new XAttribute("stroke", "#6b8ab5"), new XAttribute("stroke-width", 1)));
        }
        return Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));
    }
}
