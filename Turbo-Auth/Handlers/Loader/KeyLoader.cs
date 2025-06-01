using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Handlers.keyPool;

namespace Turbo_Auth.Handlers.Loader;

public class KeyLoader: IKeyLoader
{
    private KeyContext _keyContext;
    private IKeyPoolRepository _keyPoolRepository;
    public KeyLoader(KeyContext keyContext, IKeyPoolRepository keyPoolRepository)
    {
        _keyContext = keyContext;
        _keyPoolRepository = keyPoolRepository;
    }
    public async Task LoadKeys()
    {
        var keys = await _keyContext.SupplierKeys!
            .Where(k=>k.Enable == true)
            .Include(k => k.ModelKeyBinds)!
            .ThenInclude(k => k.Model)
            .ToListAsync();
        await _keyPoolRepository.Replace(keys);
        Console.WriteLine("loads success!");
    }
}