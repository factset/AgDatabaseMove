namespace AgDatabaseMove.Exceptions
{
  public class MissingLoginException : AgDatabaseMoveException
  {
    public MissingLoginException(string message) : base(message) { }
  }
}