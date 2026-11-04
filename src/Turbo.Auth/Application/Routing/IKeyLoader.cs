namespace Turbo.Auth.Application.Routing;

public interface IKeyLoader
{
    public Task LoadKeys(CancellationToken cancellationToken = default);
}