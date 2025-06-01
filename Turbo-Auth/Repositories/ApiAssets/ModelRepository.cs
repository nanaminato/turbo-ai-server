using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Repositories.ApiAssets;

public class ModelRepository: IModelRepository
{
    private KeyContext _keyContext;

    public ModelRepository(KeyContext keyContext)
    {
        _keyContext = keyContext;
    }

    public async Task<List<Model>?> GetModelsAsync()
    {
        return await _keyContext.Models!.ToListAsync();
    }

    public async Task<List<Model>?> GetChatModelsAsync()
    {
        return await _keyContext.Models!.Where(m => m.IsChatModel == true).ToListAsync();
    } 

    public async Task<List<Model>?> GetModelsOfKeyAsync(int keyId)
    {
        return await _keyContext.ModelKeyBinds!.Where(f => f.SupplierKeyId == keyId)
            .Include(f => f.Model)
            .Select(f => f.Model!)
            .ToListAsync();
    }

    public async Task DeleteModelByIdAsync(int id)
    {
        var modelKeyBinds = await _keyContext.ModelKeyBinds!
            .Where(f => f.ModelId == id)
            .ToListAsync();
        if (modelKeyBinds.Count != 0)
        {
            _keyContext.ModelKeyBinds!.RemoveRange(modelKeyBinds);
            await _keyContext.SaveChangesAsync();
        }
        var model = await _keyContext.Models!.Where(m => m.ModelId == id).FirstOrDefaultAsync();
        if (model != null)
        {
            _keyContext.Models!.Remove(model);
        }

        await _keyContext.SaveChangesAsync();
    }

    public async Task AddModelAsync(Model model)
    {
        var exists = await _keyContext.Models!.AnyAsync(m => m.Name == model.Name&&m.ModelValue==model.ModelValue);
        if (!exists)
        {
            _keyContext.Models!.Add(new Model()
            {
                Name = model.Name,
                Enable = model.Enable,
                IsChatModel = model.IsChatModel,
                Vision = model.Vision,
                ModelValue = model.ModelValue
            });
            await _keyContext.SaveChangesAsync();
        }
    }

    public async Task UpdateModelAsync(Model model)
    {
        var innerModel = await _keyContext.Models!
            .FirstOrDefaultAsync(m => m.ModelId == model.ModelId);
        if (innerModel != null)
        {
            innerModel.Name = model.Name;
            innerModel.IsChatModel = model.IsChatModel;
            innerModel.Enable = model.Enable;
            innerModel.ModelValue = model.ModelValue;
            innerModel.Vision = model.Vision;
            await _keyContext.SaveChangesAsync();
        }
    }

    public async Task<Model?> GetModelByName(string name)
    {
        return await _keyContext.Models!.FirstOrDefaultAsync(m => m.Name == name);
    }

    public async Task SetEnableStatus(int id, bool enable)
    {
        var modelKeyBinds = await _keyContext.ModelKeyBinds!.Where(mkb => mkb.ModelId == id)
            .ToListAsync();
        foreach (var mkb in modelKeyBinds)
        {
            mkb.Enable = enable;
            Console.WriteLine(enable);
        }

        await _keyContext.SaveChangesAsync();
    }
}