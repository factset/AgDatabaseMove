namespace AgDatabaseMove.Exceptions
{
  using System;


  /// <summary>
  ///   A base exception for the AgDatabaseMove library
  /// </summary>
  public class MissingLoginException : AgDatabaseMoveException
  {
    public MissingLoginException(string message) : base(message) { }
  }
}