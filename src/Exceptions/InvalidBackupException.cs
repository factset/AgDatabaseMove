namespace AgDatabaseMove.Exceptions
{
  public class InvalidBackupException : AgDatabaseMoveException
  {
    public InvalidBackupException(string message) : base(message) { }
  }
}