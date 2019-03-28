namespace AgDatabaseMove.Exceptions
{
  using System;


  /// <summary>
  ///   A base exception for the AgDatabaseMove library
  /// </summary>
  public class AgDatabaseMoveException : Exception
  {
    public AgDatabaseMoveException() { }

    public AgDatabaseMoveException(string message) : base(message) { }

    public AgDatabaseMoveException(string message, Exception innerException) : base(message, innerException) { }
  }
}