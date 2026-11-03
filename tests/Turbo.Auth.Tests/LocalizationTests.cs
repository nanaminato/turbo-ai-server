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
}

