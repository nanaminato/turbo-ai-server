using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Exceptions;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Repositories.Catalog;

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
            var incoming = key.ModelKeyBinds ?? [];
            if (incoming.GroupBy(binding => binding.ModelId).Any(group => group.Count() > 1))
                throw new ArgumentException("Duplicate model bindings.");
            var remaining = existingFees.ToList();
            foreach (var binding in incoming)
            {
                var existing = remaining.FirstOrDefault(item => item.ModelId == binding.ModelId);
                if (existing is null)
                {
                    binding.SupplierKeyId = innerKey.SupplierKeyId;
                    _keyContext.ModelKeyBinds!.Add(binding);
                    continue;
                }
                remaining.Remove(existing);
                existing.Enable = binding.Enable;
                existing.Fee = binding.Fee;
                existing.RoutingWeight = binding.RoutingWeight;
                existing.Priority = binding.Priority;
                existing.ProviderModelValue = binding.ProviderModelValue;
            }
            _keyContext.ModelKeyBinds!.RemoveRange(remaining);

            await _keyContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            throw new EntityNotFoundException();
        }
    }
}