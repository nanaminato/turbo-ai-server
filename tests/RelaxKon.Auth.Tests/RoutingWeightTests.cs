using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Models.Suppliers;
using RelaxKon.Auth.Contracts.Catalog;
using System.ComponentModel.DataAnnotations;
namespace RelaxKon.Auth.Tests;
public class RoutingWeightTests
{
    [TestCase(0, 1)] [TestCase(0.249999, 1)] [TestCase(0.25, 2)] [TestCase(0.999999, 2)]
    public void Selection_obeys_exact_boundaries_and_priority(double sample, int expected)
    {
        WeightKey[] routes = [new() {RouteId=1,Weight=1},new() {RouteId=2,Weight=3},new() {RouteId=3,Weight=100,Priority=1}];
        Assert.That(QuickModel.SelectWeightedRoute(routes,sample).RouteId,Is.EqualTo(expected));
    }
    [Test]
    public void Selection_scales_huge_weights_and_excludes_non_finite_values()
    {
        WeightKey[] routes = [new() {RouteId=1,Weight=double.MaxValue},new() {RouteId=2,Weight=double.MaxValue},
            new() {RouteId=3,Weight=double.PositiveInfinity},new() {RouteId=4,Weight=double.NaN},new() {RouteId=5,Weight=0}];
        Assert.That(QuickModel.SelectWeightedRoute(routes,.49).RouteId,Is.EqualTo(1));
        Assert.That(QuickModel.SelectWeightedRoute(routes,.5).RouteId,Is.EqualTo(2));
    }
    [Test]
    public async Task Explicit_weight_overrides_fee_and_zero_excludes_route()
    {
        var key=new SupplierKey {Enable=true};
        key.ModelKeyBinds=[new() {Enable=true,Fee=.01,RoutingWeight=0,SupplierKey=key,Model=new() {ModelValue="zero"}},
            new() {Enable=true,Fee=100,RoutingWeight=3,SupplierKey=key,Model=new() {ModelValue="weighted"}},
            new() {Enable=true,Fee=double.Epsilon,SupplierKey=key,Model=new() {ModelValue="tiny"}}];
        var built=await new ModelKeyBuilder().Build([key]); var active=new QuickModel();active.Transfer(built);
        Assert.That(active.IsModelAvailable("zero"),Is.False);
        Assert.That(active.GetQuick()["weighted"].Single().Weight,Is.EqualTo(3));
        Assert.That(active.IsModelAvailable("tiny"),Is.True);
        key.Enable=false; built=await new ModelKeyBuilder().Build([key]);active.Transfer(built);
        Assert.That(active.GetQuick(),Is.Empty);
    }
    [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(1000001)]
    public void Contract_rejects_invalid_weights(double weight)
    {
        var request=new ModelBindingRequest {ModelId=1,RoutingWeight=weight};
        Assert.That(Validator.TryValidateObject(request,new ValidationContext(request),[],true),Is.False);
    }
}
