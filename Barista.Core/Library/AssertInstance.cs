﻿namespace Barista.Library
{
  using Barista.Jurassic;
  using Barista.Jurassic.Library;
  using Microsoft.VisualStudio.TestTools.UnitTesting;
  using System;

  [Serializable]
  public class AssertConstructor : ClrFunction
  {
    public AssertConstructor(ScriptEngine engine)
      : base(engine.Function.InstancePrototype, "Assert", new AssertInstance(engine.Object.InstancePrototype))
    {
    }

    [JSConstructorFunction]
    public AssertInstance Construct()
    {
      return new AssertInstance(this.InstancePrototype);
    }
  }

  [Serializable]
  public class AssertInstance : ObjectInstance
  {
    public AssertInstance(ObjectInstance prototype)
      : base(prototype)
    {
      this.PopulateFields();
      this.PopulateFunctions();
    }

    [JSFunction(Name = "areEqual")]
    public void AreEqual(object expected, object actual, object message)
    {
      if (TypeUtilities.IsString(expected))
        expected = TypeConverter.ToString(expected);

      if (TypeUtilities.IsString(actual))
        actual = TypeConverter.ToString(actual);

      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);

        try
        {
          Assert.AreEqual(expected, actual, strMessage);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
        
      }
      else
      {
        try
        {
          Assert.AreEqual(expected, actual);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "areDeepEqual")]
    public void AreDeepEqual(object expected, object actual, object message)
    {
      var bat = this.Engine.GetGlobalValue("BaristaUnitTesting") as ObjectInstance;
      if (bat == null)
        throw new InvalidOperationException("Unable to locate BaristaUnitTesting object.");

      var result = TypeConverter.ToBoolean(bat.CallMemberFunction("equiv", expected, actual));
      var strMessage = TypeConverter.ToString(message);

      try
      {
        Assert.IsTrue(result, strMessage);
      }
      catch (AssertFailedException ex)
      {
        HandleAssertFailedException(ex);
      }
    }

    [JSFunction(Name = "areNotEqual")]
    public void AreNotEqual(object expected, object actual, object message)
    {
      if (TypeUtilities.IsString(expected))
        expected = TypeConverter.ToString(expected);

      if (TypeUtilities.IsString(actual))
        actual = TypeConverter.ToString(actual);

      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.AreNotEqual(expected, actual, strMessage);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
      else
      {
        try
        {
          Assert.AreNotEqual(expected, actual);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "areNotDeepEqual")]
    public void AreNotDeepEqual(object expected, object actual, object message)
    {
      var bat = this.Engine.GetGlobalValue("BaristaUnitTesting") as ObjectInstance;
      if (bat == null)
        throw new InvalidOperationException("Unable to locate BaristaUnitTesting object.");

      var result = TypeConverter.ToBoolean(bat.CallMemberFunction("equiv", expected, actual));
      var strMessage = TypeConverter.ToString(message);

      try
      {
        Assert.IsTrue(!result, strMessage);
      }
      catch (AssertFailedException ex)
      {
        HandleAssertFailedException(ex);
      }
    }

    [JSFunction(Name = "areSame")]
    public void AreSame(object expected, object actual, object message)
    {
      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.AreSame(expected, actual, strMessage);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
      else
      {
        try
        {
          Assert.AreSame(expected, actual);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "areNotSame")]
    public void AreNotSame(object expected, object actual, object message)
    {
      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.AreNotSame(expected, actual, strMessage);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
      else
      {
        try
        {
          Assert.AreNotEqual(expected, actual);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "fail")]
    public void Fail(object message)
    {
      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.Fail(strMessage);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
      else
      {
        try
        {
          Assert.Fail();
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "inconclusive")]
    public void Inconclusive(object message)
    {
      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.Inconclusive(strMessage);
        }
        catch (AssertInconclusiveException ex)
        {
          HandleAssertInconclusiveException(ex);
        }
      }
      else
      {
        try
        {
          Assert.Inconclusive();
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "isFalse")]
    public void IsFalse(object condition, object message)
    {
      var bCondition = TypeConverter.ToBoolean(condition);

      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.IsFalse(bCondition, strMessage);
        }
        catch (AssertInconclusiveException ex)
        {
          HandleAssertInconclusiveException(ex);
        }
      }
      else
      {
        try
        {
          Assert.IsFalse(bCondition);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "isTrue")]
    public void IsTrue(object condition, object message)
    {
      var bCondition = TypeConverter.ToBoolean(condition);

      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.IsTrue(bCondition, strMessage);
        }
        catch (AssertInconclusiveException ex)
        {
          HandleAssertInconclusiveException(ex);
        }
      }
      else
      {
        try
        {
          Assert.IsTrue(bCondition);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "isNull")]
    public void IsNull(object value, object message)
    {
      if (value == Null.Value || value == Undefined.Value)
        value = null;

      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.IsNull(value, strMessage);
        }
        catch (AssertInconclusiveException ex)
        {
          HandleAssertInconclusiveException(ex);
        }
      }
      else
      {
        try
        {
          Assert.IsNull(value);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "isNotNull")]
    public void IsNotNull(object value, object message)
    {
      if (value == Null.Value || value == Undefined.Value)
        value = null;

      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.IsNotNull(value, strMessage);
        }
        catch (AssertInconclusiveException ex)
        {
          HandleAssertInconclusiveException(ex);
        }
      }
      else
      {
        try
        {
          Assert.IsNotNull(value);
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "isUndefined")]
    public void IsUndefined(object value, object message)
    {
      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.IsTrue(TypeUtilities.IsUndefined(value), strMessage);
        }
        catch (AssertInconclusiveException ex)
        {
          HandleAssertInconclusiveException(ex);
        }
      }
      else
      {
        try
        {
          Assert.IsTrue(TypeUtilities.IsUndefined(value));
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    [JSFunction(Name = "isNotUndefined")]
    public void IsNotUndefined(object value, object message)
    {
      if (message != null && message != Null.Value && message != Undefined.Value)
      {
        var strMessage = TypeConverter.ToString(message);
        try
        {
          Assert.IsTrue(!TypeUtilities.IsUndefined(value), strMessage);
        }
        catch (AssertInconclusiveException ex)
        {
          HandleAssertInconclusiveException(ex);
        }
      }
      else
      {
        try
        {
          Assert.IsTrue(!TypeUtilities.IsUndefined(value));
        }
        catch (AssertFailedException ex)
        {
          HandleAssertFailedException(ex);
        }
      }
    }

    private void HandleAssertFailedException(AssertFailedException ex)
    {
      var jsEx = new JavaScriptException(this.Engine, "Error", ex.Message, ex);
      throw jsEx;
    }

    private void HandleAssertInconclusiveException(AssertInconclusiveException ex)
    {
      var jsEx = new JavaScriptException(this.Engine, "Error", ex.Message, ex);
      throw jsEx;
    }
  }
}
