﻿namespace Barista.SocketBase.Command
{
  using Barista.SocketBase.Protocol;

  /// <summary>
  /// A command type for whose request info type is StringRequestInfo
  /// </summary>
  /// <typeparam name="TAppSession">The type of the app session.</typeparam>
  public abstract class StringCommandBase<TAppSession> : CommandBase<TAppSession, StringRequestInfo>
      where TAppSession : IAppSession, IAppSession<TAppSession, StringRequestInfo>, new()
  {

  }

  /// <summary>
  /// A command type for whose request info type is StringRequestInfo
  /// </summary>
  public abstract class StringCommandBase : StringCommandBase<AppSession>
  {

  }
}
