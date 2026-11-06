using Microsoft.EntityFrameworkCore;
using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Models.Suppliers;
using RelaxKon.Auth.Repositories.Catalog;
namespace RelaxKon.Auth.Tests;
public class RoutingWeightDatabaseTests
{
    [Test,Explicit("Requires isolated MySQL database.")]
    public async Task Catalog_refresh_is_idempotent_and_preserves_custom_examples()
    {
        await using var db=await MySqlTestDatabase.Create();
        await db.Execute("INSERT INTO AvailableModels(Enable,Name,IsChatModel,ModelValue,Vision) VALUES (1,'Old',1,'chatgpt-4o-latest',1),(1,'Custom',1,'my-custom-model',0)");
        await db.Execute(MySqlTestDatabase.Resource("ModelCatalogRefresh.sql"));
        await db.Execute(MySqlTestDatabase.Resource("ModelCatalogRefresh.sql"));
        Assert.That(await db.Scalar<long>("SELECT COUNT(*) FROM AvailableModels"),Is.EqualTo(6));
        Assert.That(await db.Scalar<long>("SELECT COUNT(*) FROM AvailableModels WHERE ModelValue='my-custom-model'"),Is.EqualTo(1));
        Assert.That(await db.Scalar<long>("SELECT COUNT(*) FROM AvailableModels WHERE ModelValue='chatgpt-4o-latest'"),Is.Zero);
        Assert.That(await db.Scalar<long>("SELECT COUNT(*) FROM ChatMessages"),Is.EqualTo(2));
    }
    [TestCase(false),TestCase(true),Explicit("Requires isolated MySQL database.")]
    public async Task Weight_updates_preserve_route_identity_and_refresh_revision(bool legacy)
    {
        await using var db=await MySqlTestDatabase.Create(legacy:legacy);
        await db.Execute("""
            INSERT INTO Models(ModelId,Name,IsChatModel,ModelValue,Vision) VALUES (1,'logical',1,'logical',1);
            INSERT INTO SupplierKeys(SupplierKeyId,BaseUrl,RequestIdentifier,Enable,ApiKey) VALUES (1,'https://example.invalid',0,1,'fake');
            INSERT INTO ModelKeyBinds(ModelKeyBindId,Enable,SupplierKeyId,ModelId,Fee,ProviderModelValue,Priority) VALUES (12,1,1,1,4,'provider',0);
            """);
        var before=await db.Scalar<long>("SELECT Version FROM RoutingConfiguration WHERE Id=1");
        await using(var context=db.Keys())
        {
            await new KeyRepository(context).UpdateKeyAsync(new SupplierKey {SupplierKeyId=1,Enable=true,BaseUrl="https://example.invalid",ApiKey="fake",
                ModelKeyBinds=[new() {ModelId=1,Enable=true,Fee=4,RoutingWeight=0,ProviderModelValue="alias",Priority=2}]});
        }
        Assert.That(await db.Scalar<long>("SELECT ModelKeyBindId FROM ModelKeyBinds"),Is.EqualTo(12));
        Assert.That(await db.Scalar<long>("SELECT Version FROM RoutingConfiguration WHERE Id=1"),Is.GreaterThan(before));
        await using(var context=db.Keys())
        {
            var keys=await context.SupplierKeys!.Include(k=>k.ModelKeyBinds)!.ThenInclude(b=>b.Model).ToListAsync();
            var active=new QuickModel();active.Transfer(await new ModelKeyBuilder().Build(keys));
            Assert.That(active.IsModelAvailable("logical"),Is.False);
        }
        await db.Execute("UPDATE ModelKeyBinds SET RoutingWeight=NULL WHERE ModelKeyBindId=12");
        await using(var context=db.Keys())
        {
            var keys=await context.SupplierKeys!.Include(k=>k.ModelKeyBinds)!.ThenInclude(b=>b.Model).ToListAsync();
            var active=new QuickModel();active.Transfer(await new ModelKeyBuilder().Build(keys));
            Assert.That(active.GetQuick()["logical"].Single().Weight,Is.EqualTo(.25));
        }
    }
}
