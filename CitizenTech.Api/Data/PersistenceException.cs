namespace CitizenTech.Api.Data;

public class PersistenceException : Exception
{
    public PersistenceException(Exception innerException)
        : base("The persistence operation could not be confirmed.", innerException) { }
}

public sealed class DuplicateRecordException : PersistenceException
{
    public DuplicateRecordException(Exception innerException) : base(innerException) { }
}
