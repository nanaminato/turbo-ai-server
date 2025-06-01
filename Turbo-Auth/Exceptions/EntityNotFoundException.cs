namespace Turbo_Auth.Exceptions;

public class EntityNotFoundException : Exception 
{ 
    public EntityNotFoundException() : 
        base("The item already exists.") { }

    public EntityNotFoundException(string message) : 
        base(message)
    {
        
    }

    public EntityNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
        
    }
}