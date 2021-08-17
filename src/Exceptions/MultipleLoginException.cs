namespace AgDatabaseMove.Exceptions
{

  public class MultipleLoginException : AgDatabaseMoveException
  {
    public MultipleLoginException(string message) : base(message) { }
  }
}