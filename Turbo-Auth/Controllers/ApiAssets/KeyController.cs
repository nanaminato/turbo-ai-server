using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Models.Suppliers;
using Turbo_Auth.Repositories.ApiAssets;

namespace Turbo_Auth.Controllers.ApiAssets;
[ApiController]
[Authorize(Policy = "admin")]
[Route("api/[controller]")]
public class KeyController: Controller
{
    private IKeyRepository _keyRepository;

    public KeyController(IKeyRepository keyRepository)
    {
        _keyRepository = keyRepository;
    }
    
    [HttpGet("{keyId}")]
    public async Task<SupplierKey?> GetKeyById(int keyId)
    {
        return await _keyRepository.GetKeyByIdAsync(keyId);
    }

    [HttpGet]
    public async Task<List<SupplierKey>?> GetKeysWithModel([FromQuery] int? modelId)
    {
        if (modelId == null)
        {
            return await _keyRepository.GetKeysAsync();
        }

        return await _keyRepository.GetKeysWithModelAsync(modelId.Value);
    }

    [HttpDelete("{keyId}")]
    public async Task<IActionResult> DeleteKey(int keyId)
    {
        try
        {
            await _keyRepository.DeleteKeyByIdAsync(keyId);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest();
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddKey(SupplierKey key)
    {
        try
        {
            if (key.ModelKeyBinds != null)
            {
                foreach (var modelKeyBind in key.ModelKeyBinds)
                {
                    modelKeyBind.Model = null;
                }
            }
            await _keyRepository.AddKeyAsync(key);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest();
        }
    }

    [HttpGet("types")]
    public IActionResult GetKeyTypes()
    {
        var types = new List<KeyTypes>()
        {
            new()
            {
                Type = "OpenAI",
                RequestIdentifier = 0
            },
            new()
            {
                Type = "Google",
                RequestIdentifier = 1
            },
            new()
            {
                Type = "Anthropic",
                RequestIdentifier = 2
            },
            new ()
            {
                Type = "Novita",
                RequestIdentifier = 3
            },
            new ()
            {
                Type = "Alibaba",
                RequestIdentifier = 4
            },
            new ()
            {
                Type = "Twitter",
                RequestIdentifier = 5
            }
        };
        return Ok(types);
    }

    [HttpPut("{keyId}")]
    public async Task<IActionResult> UpdateKey(SupplierKey key)
    {
        try
        {
            if (key.ModelKeyBinds != null)
            {
                foreach (var modelKeyBind in key.ModelKeyBinds)
                {
                    modelKeyBind.Model = null;
                }
            }
            await _keyRepository.UpdateKeyAsync(key);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest();
        }
    }
}

public class KeyTypes
{
    public string? Type
    {
        get;
        set;
    }

    public int RequestIdentifier
    {
        get;
        set;
    }
}