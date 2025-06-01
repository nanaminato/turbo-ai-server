using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Exceptions;
using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Repositories.ApiAssets;

public class KeyRepository: IKeyRepository
{
    private KeyContext _keyContext;

    public KeyRepository(KeyContext keyContext)
    {
        _keyContext = keyContext;
    }

    public async Task<List<SupplierKey>?> GetKeysAsync()
    {
        return await _keyContext.SupplierKeys!.Include(k=>k.ModelKeyBinds)!.ThenInclude(f=>f.Model).ToListAsync();
    }

    public async Task<List<SupplierKey>?> GetKeysWithModelAsync(int modelId)
    {
        var keyIds = await _keyContext.ModelKeyBinds!
            .Where(f => f.ModelId == modelId)
            .Select(f => f.SupplierKeyId)
            .ToListAsync();

        var keys = await _keyContext.SupplierKeys!
            .Include(k => k.ModelKeyBinds)!
            .ThenInclude(f => f.Model)
            .Where(k => keyIds.Contains(k.SupplierKeyId))
            .ToListAsync();

        return keys;
    }

    public async Task<SupplierKey?> GetKeyByIdAsync(int id)
    {
        return await _keyContext.SupplierKeys!.Where(k => k.SupplierKeyId == id)
            .Include(k => k.ModelKeyBinds)!
            .ThenInclude(f => f.Model).FirstOrDefaultAsync();
    }

    public async Task DeleteKeyByIdAsync(int id)
    {

        var key = await _keyContext.SupplierKeys!.FirstOrDefaultAsync(k => k.SupplierKeyId == id);
        if (key != null)
        {
            _keyContext.SupplierKeys!.Remove(key);
            await _keyContext.SaveChangesAsync();
        }

        
    }

    public async Task AddKeyAsync(SupplierKey key)
    {
        try
        {
            _keyContext.SupplierKeys!.Add(key);
            await _keyContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            throw new EntityNotFoundException();
        }
    }

    public async Task UpdateKeyAsync(SupplierKey key)
    {
        try
        {
            var innerKey = await _keyContext.SupplierKeys!
                .FirstOrDefaultAsync(k 
                    => k.SupplierKeyId == key.SupplierKeyId);
            if (innerKey == null)
            {
                return;
            }

            innerKey.ApiKey = key.ApiKey;
            innerKey.BaseUrl = key.BaseUrl;
            innerKey.RequestIdentifier = key.RequestIdentifier;
            innerKey.Enable = key.Enable;
            var existingFees = await _keyContext.ModelKeyBinds!.Where(f => f.SupplierKeyId == key.SupplierKeyId).ToListAsync();
            _keyContext.ModelKeyBinds!.RemoveRange(existingFees);

            if (key.ModelKeyBinds != null && key.ModelKeyBinds.Count != 0)
            {
                foreach (var modelKeyBind in key.ModelKeyBinds)
                {
                    modelKeyBind.SupplierKeyId = innerKey.SupplierKeyId; // Set the SupplierKeyId for each fee
                }
                _keyContext.ModelKeyBinds.AddRange(key.ModelKeyBinds);
            }

            await _keyContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            throw new EntityNotFoundException();
        }
    }
}