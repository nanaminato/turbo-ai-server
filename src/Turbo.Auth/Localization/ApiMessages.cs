using System.Globalization;

namespace Turbo.Auth.Localization;

public static class ApiMessages
{
    public static string Get(string code) => (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, code) switch
    {
        ("en", "internal_error") => "Internal server error",
        ("ja", "internal_error") => "サーバー内部エラー",
        (_, "internal_error") => "服务器内部错误",
        ("en", "account_exists") => "This email and username combination already exists.",
        ("ja", "account_exists") => "このメールアドレスとユーザー名の組み合わせは既に登録されています。",
        (_, "account_exists") => "存在相同的Email+用户名组合",
        _ => code
    };
}
