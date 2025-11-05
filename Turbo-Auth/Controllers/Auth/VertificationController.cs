using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Turbo_Auth.Controllers.Auth;

[ApiController]
[Route("api/verification")]
public class VerificationController : ControllerBase
{
    [HttpGet("generate")]
    public IActionResult GetCaptcha()
    {
        var captchaCode = GenerateRandomCode(); // 生成随机验证码
        var captchaBytes = GenerateCaptchaImage(captchaCode); // 生成验证码图片

        return Ok(new { img = captchaBytes, code = captchaCode });
    }

    [HttpGet("check-token")]
    [Authorize]
    public IActionResult CheckToken()
    {
        return Ok();
    }

    private string GenerateRandomCode()
    {
        const string chars = "ABCDEFGHIJKMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var result = new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        return result;
    }


    private byte[] GenerateCaptchaImage(string code)
    {
        Image image = new Image<Rgba32>(200, 50);
        var random = new Random();
        image.Mutate(x =>
        {
            // 获取可用字体，如果 Arial 不存在则使用系统第一个可用字体
            var font = GetAvailableFont("Arial", 20);
        
            // 生成验证码图片逻辑，将code绘制在bitmap上，并添加一些干扰线、噪点等效果
            x.DrawText(code, font, Color.White, new PointF(random.NextInt64(20,100), random.NextInt64(10,20)));

            for (var i = 0; i < 5; i++)
            {
                Star star = new(x: random.NextInt64(0,200), y: random.NextInt64(0,50), prongs: 3, innerRadii: 2.0f, outerRadii:3.0f);
                image.Mutate(c => c.Fill(Color.Blue, star));
            }
        });

        using var memoryStream = new MemoryStream();
        image.SaveAsJpeg(memoryStream);
        return memoryStream.ToArray();
    }

    private Font GetAvailableFont(string preferredFont, float size, FontStyle style = FontStyle.Regular)
    {
        try
        {
            // 首先尝试首选字体
            return SystemFonts.CreateFont(preferredFont, size, style);
        }
        catch (FontFamilyNotFoundException)
        {
            try
            {
                // 尝试常见的备用字体（跨平台）
                var fallbackFonts = new[]
                {
                    "DejaVu Sans", // Linux 常见
                    "Liberation Sans", // Linux 替代 Arial
                    "Times New Roman", // Windows 常见
                    "Helvetica", // macOS 常见
                    "Ubuntu", // Ubuntu 系统字体
                    "FreeSans", // 免费字体
                    "Nimbus Sans", // 另一个 Linux 字体
                    "Microsoft Sans Serif" // Windows 备用
                };

                foreach (var fontName in fallbackFonts)
                {
                    try
                    {
                        return SystemFonts.CreateFont(fontName, size, style);
                    }
                    catch (FontFamilyNotFoundException)
                    {
                        // 继续尝试下一个字体
                        continue;
                    }
                }

                // 如果所有备用字体都不存在，使用系统第一个可用字体
                var availableFonts = SystemFonts.Families.ToArray();
                if (availableFonts.Length > 0)
                {
                    return SystemFonts.CreateFont(availableFonts[0].Name, size, style);
                }

                // 如果没有任何字体，抛出异常
                throw new InvalidOperationException("No fonts available on the system");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to find any usable font: {ex.Message}");
            }
        }
    }
}
