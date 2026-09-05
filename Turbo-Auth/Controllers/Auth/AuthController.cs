using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Turbo_Auth.Context;
using Turbo_Auth.Controllers.Auth.Body;
using Turbo_Auth.Models.Accounts;
using Turbo_Auth.Repositories.Accounts;
using Turbo_Auth.Security;

namespace Turbo_Auth.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : Controller
{
    private AuthContext _context;
    private readonly IConfiguration _configuration;
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountPasswordService _passwordService;
    public AuthController(AuthContext context,IConfiguration configuration,
        IAccountRepository accountRepository, IAccountPasswordService passwordService)
    {
        _context = context;
        _configuration = configuration;
        _accountRepository = accountRepository;
        _passwordService = passwordService;
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] SignBody body)
    {
        var username = body.Username;
        var password = body.Password;
        var res = await IsValidUser(username!, password!);
        if (res.Item1)
        {
            var token = GenerateToken(username!, res.Item2);
            return Ok(new { Token = token,Id = res.Item2 });
        }

        return Unauthorized();
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterBody body)
    {
        var count = await _context.Accounts!.Where(u => u.Email == body.Email && u.Username == body.Username)
            .CountAsync();
        if (count >= 1)
        {
            return Conflict("存在相同的Email+用户名组合");
        }

        var account = new Account()
        {
            Email = body.Email,
            Username = body.Username,
            Password = body.Password
        };
        try
        {
            await _accountRepository.AddUserAccountAsync(account);
            return Ok();
        }
        catch (Exception)
        {
            return BadRequest("服务端错误");
        }
    }
    
    private async Task<(bool,int)> IsValidUser(string username, string password)
    {
        var user = await _context.Accounts!.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return (false,-1);

        var verification = _passwordService.Verify(user, password);
        if (verification == PasswordVerificationState.Invalid) return (false, -1);

        if (verification == PasswordVerificationState.ValidNeedsUpgrade)
        {
            user.Password = _passwordService.Hash(user, password);
            await _context.SaveChangesAsync();
        }

        return (true,user.AccountId);
    }
    
    private string GenerateToken(string username, int id)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var roles = _context.AccountRoles!.Where(a => a.AccountId == id)
            .Include(r=>r.Role).ToList();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, $"{username}"),
            new(ClaimTypes.NameIdentifier, id + "")
        };
        roles.ForEach(role =>
        {
            claims.Add(new Claim(ClaimTypes.Role,role.Role!.Name!));
        });
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(30),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    
}
