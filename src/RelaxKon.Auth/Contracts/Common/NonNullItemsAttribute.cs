using System.Collections;
using System.ComponentModel.DataAnnotations;
namespace RelaxKon.Auth.Contracts.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NonNullItemsAttribute : ValidationAttribute
{
    public NonNullItemsAttribute() : base("Collections must not contain null entries.") { }
    public override bool IsValid(object? value) => value is null ||
        (value is IDictionary dictionary ? dictionary.Values : (IEnumerable)value).Cast<object?>().All(item => item is not null);
}
