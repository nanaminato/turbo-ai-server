namespace Turbo_Auth.Exceptions;

public class AlreadyExistException : Exception 
{ 
    public AlreadyExistException() : 
        base("The item already exists.") { }

    public AlreadyExistException(string message) : 
        base(message)
    {
        
    }

    public AlreadyExistException(string message, Exception innerException) : base(message, innerException)
    {
        
    }
}