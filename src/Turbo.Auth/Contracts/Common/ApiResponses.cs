namespace Turbo.Auth.Contracts.Common;

public sealed record CodeResponse(string Code);
public sealed record MessageResponse(string Msg);
public sealed record ApiMessageResponse(string Code, string Message);
