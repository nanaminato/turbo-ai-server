using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Auth.Localization;

namespace Turbo.Auth.Tests;

public class LocalizationTests
{
    [TestCase("en-US", "en", "Internal server error")]
    [TestCase("ja-JP", "ja", "サーバー内部エラー")]
    [TestCase("zh-CN", "zh", "服务器内部错误")]
    [TestCase("fr", "zh", "服务器内部错误")]
    [TestCase("fr;q=1,en;q=0.8", "en", "Internal server error")]
    [TestCase("", "zh", "服务器内部错误")]
    public async Task Header_selects_language_and_returns_content_language(string header, string language, string message)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers.AcceptLanguage = header;
        var options = new RequestLocalizationOptions()
            .SetDefaultCulture("zh").AddSupportedCultures("zh", "en", "ja")
            .AddSupportedUICultures("zh", "en", "ja");
        options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        options.ApplyCurrentCultureToResponseHeaders = true;
        var middleware = new RequestLocalizationMiddleware(_ =>
        {
            Assert.That(CultureInfo.CurrentUICulture.Name, Is.EqualTo(language));
            Assert.That(ApiMessages.Get("internal_error"), Is.EqualTo(message));
            return Task.CompletedTask;
        }, Microsoft.Extensions.Options.Options.Create(options), services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        await middleware.Invoke(context);
        Assert.That(context.Response.Headers.ContentLanguage.ToString(), Is.EqualTo(language));
    }

    [TestCase("en", "A model must be specified.")]
    [TestCase("ja", "モデルを指定してください。")]
    [TestCase("zh", "必须指定 model。")]
    public async Task Chat_validation_returns_a_localized_problem(string language, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            var context = new DefaultHttpContext();
            context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
            context.Response.Body = new MemoryStream();
            var controller = new Turbo.Auth.Controllers.AI.Chat.ChatController(null!, null!, null!, null!, null!,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Turbo.Auth.Controllers.AI.Chat.ChatController>.Instance)
            {
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = context }
            };
            await controller.Chat(new Turbo.Auth.Controllers.AI.Chat.Models.NoModelChatBody());
            context.Response.Body.Position = 0;
            using var problem = await System.Text.Json.JsonDocument.ParseAsync(context.Response.Body);
            Assert.That(context.Response.StatusCode, Is.EqualTo(400));
            Assert.That(problem.RootElement.GetProperty("title").GetString(), Is.EqualTo(expected));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}

