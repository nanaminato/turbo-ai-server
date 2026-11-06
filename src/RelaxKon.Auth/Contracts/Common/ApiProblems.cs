using Microsoft.AspNetCore.Mvc;
using RelaxKon.Auth.Localization;
namespace RelaxKon.Auth.Contracts.Common;

public static class ApiProblems
{
    public static ProblemDetails Create(int status, string code) => new()
    {
        Status = status,
        Title = ApiMessages.Get(code),
        Extensions = { ["code"] = code }
    };
}
