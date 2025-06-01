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
            // "Arial" "Times New Roman"
            var fonts = SystemFonts.CreateFont("Arial",20);
            // 生成验证码图片逻辑，将code绘制在bitmap上，并添加一些干扰线、噪点等效果
            x.DrawText(code, fonts, Color.White, new PointF(random.NextInt64(20,100), random.NextInt64(10,20)));

            for (var i = 0; i < 5; i++)
            {
                
                Star star = new(x: random.NextInt64(0,200), y: random.NextInt64(0,50), prongs: 3, innerRadii: 2.0f, outerRadii:3.0f);

                image.Mutate( c=> c.Fill(Color.Blue, star));
            }

        });

        using var memoryStream = new MemoryStream();
        image.SaveAsJpeg(memoryStream);
        return memoryStream.ToArray();
    }

}
