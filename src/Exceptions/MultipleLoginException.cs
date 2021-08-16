namespace AgDatabaseMove.Exceptions
{
  using System;


  /// <summary>
  ///   A base exception for the AgDatabaseMove library
  /// </summary>
  public class MultipleLoginException : AgDatabaseMoveException
  {
    public MultipleLoginException(string message) : base(message) { }
  }
}