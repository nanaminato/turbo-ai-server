namespace Turbo_Auth.Exceptions;

public class NotMatchException : Exception 
{ 
    public NotMatchException() : 
        base("数据库操作失败") { }

    public NotMatchException(string message) : 
        base(message)
    {
        
    }

    public NotMatchException(string message, Exception innerException) : base(message, innerException)
    {
        
    }
}