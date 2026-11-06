namespace RelaxKon.Auth.Application.Routing;

public interface IKeyLoader
{
    public Task LoadKeys(CancellationToken cancellationToken = default);
}