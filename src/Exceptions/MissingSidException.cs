namespace AgDatabaseMove.Exceptions
{
  using System;


  /// <summary>
  ///   A base exception for the AgDatabaseMove library
  /// </summary>
  public class MissingSidException : AgDatabaseMoveException
  {
    public MissingSidException(string message) : base(message) { }
  }
}