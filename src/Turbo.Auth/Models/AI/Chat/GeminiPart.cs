using DotnetGeminiSDK.Model.Response;

namespace Turbo.Auth.Models.AI.Chat;

public class GeminiPart
{
    public Candidate?[]? Candidates
    {
        get;
        set;
    }
}