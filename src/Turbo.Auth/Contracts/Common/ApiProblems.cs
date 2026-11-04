using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Localization;
namespace Turbo.Auth.Contracts.Common;

public static class ApiProblems
{
    public static ProblemDetails Create(int status, string code) => new()
    {
        Status = status,
        Title = ApiMessages.Get(code),
        Extensions = { ["code"] = code }
    };
}
