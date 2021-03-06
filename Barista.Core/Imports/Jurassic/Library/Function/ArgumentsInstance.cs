﻿namespace Barista.Jurassic.Library
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using Jurassic.Compiler;

  /// <summary>
  /// Represents an arguments object.
  /// </summary>
  [Serializable]
  public class ArgumentsInstance : ObjectInstance
  {
    private readonly UserDefinedFunction m_callee;
    private readonly DeclarativeScope m_scope;
    private readonly bool[] m_mappedArguments;

    //     INITIALIZATION
    //_________________________________________________________________________________________

    /// <summary>
    /// Creates a new Arguments instance.
    /// </summary>
    /// <param name="prototype"> The next object in the prototype chain. </param>
    /// <param name="callee"> The function that was called. </param>
    /// <param name="scope"> The function scope. </param>
    /// <param name="argumentValues"> The argument values that were passed to the function. </param>
    public ArgumentsInstance(ObjectInstance prototype, UserDefinedFunction callee, DeclarativeScope scope, object[] argumentValues)
      : base(prototype)
    {
      if (callee == null)
        throw new ArgumentNullException("callee");
      if (scope == null)
        throw new ArgumentNullException("scope");
      if (argumentValues == null)
        throw new ArgumentNullException("argumentValues");
      this.m_callee = callee;
      this.m_scope = scope;
      this.FastSetProperty("length", argumentValues.Length, PropertyAttributes.NonEnumerable, false);

      if (this.m_callee.StrictMode == false)
      {
        this.FastSetProperty("callee", callee, PropertyAttributes.NonEnumerable, false);


        // Create an array mappedArguments where mappedArguments[i] = true means a mapping is
        // maintained between arguments[i] and the corresponding variable.
        this.m_mappedArguments = new bool[argumentValues.Length];
        var mappedNames = new Dictionary<string, int>();    // maps argument name -> index
        for (int i = 0; i < argumentValues.Length; i++)
        {
          if (i < callee.ArgumentNames.Count)
          {
            // Check if the argument name appeared previously in the argument list.
            int previousIndex;
            if (mappedNames.TryGetValue(callee.ArgumentNames[i], out previousIndex))
            {
              // The argument name has appeared before.  Remove the getter/setter.
              this.DefineProperty(previousIndex.ToString(), new PropertyDescriptor(argumentValues[previousIndex], PropertyAttributes.FullAccess), false);

              // The argument is no longer mapped.
              this.m_mappedArguments[previousIndex] = false;
            }

            // Add the argument name and index to the hashtable.
            mappedNames[callee.ArgumentNames[i]] = i;

            // The argument is mapped by default.
            this.m_mappedArguments[i] = true;

            // Define a getter and setter so that the property value reflects that of the argument.
            var getter = new UserDefinedFunction(this.Engine.Function.InstancePrototype, "ArgumentGetter", new string[0], this.m_scope, "return " + callee.ArgumentNames[i], ArgumentGetter, true);
            getter.SetPropertyValue("argumentIndex", i, false);
            var setter = new UserDefinedFunction(this.Engine.Function.InstancePrototype, "ArgumentSetter", new[] { "value" }, this.m_scope, callee.ArgumentNames[i] + " = value", ArgumentSetter, true);
            setter.SetPropertyValue("argumentIndex", i, false);
            this.DefineProperty(i.ToString(), new PropertyDescriptor(getter, setter, PropertyAttributes.FullAccess), false);
          }
          else
          {
            // This argument is unnamed - no mapping needs to happen.
            this[(uint)i] = argumentValues[i];
          }
        }
      }
      else
      {
        // In strict mode, arguments items are not connected to the variables.
        for (int i = 0; i < argumentValues.Length; i++)
          this[(uint)i] = argumentValues[i];

        // In strict mode, accessing caller or callee is illegal.
        var throwErrorFunction = new ThrowTypeErrorFunction(this.Engine.Function.InstancePrototype);
        this.DefineProperty("caller", new PropertyDescriptor(throwErrorFunction, throwErrorFunction, PropertyAttributes.Sealed), false);
        this.DefineProperty("callee", new PropertyDescriptor(throwErrorFunction, throwErrorFunction, PropertyAttributes.Sealed), false);
      }
    }



    //     OVERRIDES
    //_________________________________________________________________________________________

    /// <summary>
    /// Gets the internal class name of the object.  Used by the default toString()
    /// implementation.
    /// </summary>
    protected override string InternalClassName
    {
      get { return "Arguments"; }
    }

    /// <summary>
    /// Used to retrieve the value of an argument.
    /// </summary>
    /// <param name="engine"> The associated script engine. </param>
    /// <param name="scope"> The scope (global or eval context) or the parent scope (function
    /// context). </param>
    /// <param name="thisObject"> The value of the <c>this</c> keyword. </param>
    /// <param name="functionObject"> The function object. </param>
    /// <param name="argumentValues"> The arguments that were passed to the function. </param>
    /// <returns> The result of calling the method. </returns>
    private object ArgumentGetter(ScriptEngine engine, Compiler.Scope scope, object thisObject, Library.FunctionInstance functionObject, object[] argumentValues)
    {
      int argumentIndex = TypeConverter.ToInteger(functionObject.GetPropertyValue("argumentIndex"));
      return this.m_scope.GetValue(this.m_callee.ArgumentNames[argumentIndex]);
    }

    /// <summary>
    /// Used to set the value of an argument.
    /// </summary>
    /// <param name="engine"> The associated script engine. </param>
    /// <param name="scope"> The scope (global or eval context) or the parent scope (function
    /// context). </param>
    /// <param name="thisObject"> The value of the <c>this</c> keyword. </param>
    /// <param name="functionObject"> The function object. </param>
    /// <param name="argumentValues"> The arguments that were passed to the function. </param>
    /// <returns> The result of calling the method. </returns>
    private object ArgumentSetter(ScriptEngine engine, Compiler.Scope scope, object thisObject, Library.FunctionInstance functionObject, object[] argumentValues)
    {
      int argumentIndex = TypeConverter.ToInteger(functionObject.GetPropertyValue("argumentIndex"));
      if (argumentValues != null && argumentValues.Length >= 1)
      {
        object value = argumentValues[0];
        this.m_scope.SetValue(this.m_callee.ArgumentNames[argumentIndex], value);
      }
      return Undefined.Value;
    }

    /// <summary>
    /// Deletes the property with the given array index.
    /// </summary>
    /// <param name="index"> The array index of the property to delete. </param>
    /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
    /// be set because the property was marked as non-configurable.  </param>
    /// <returns> <c>true</c> if the property was successfully deleted, or if the property did
    /// not exist; <c>false</c> if the property was marked as non-configurable and
    /// <paramref name="throwOnError"/> was <c>false</c>. </returns>
    public override bool Delete(uint index, bool throwOnError)
    {
      // Break the mapping between the array element of this object and the corresponding
      // variable name.
      if (index < this.m_mappedArguments.Length && this.m_mappedArguments[index])
      {
        this.m_mappedArguments[index] = false;
        var currentValue = this.m_scope.GetValue(this.m_callee.ArgumentNames[(int)index]);
        DefineProperty(index.ToString(CultureInfo.InvariantCulture), new PropertyDescriptor(currentValue, PropertyAttributes.FullAccess), false);
      }

      // Delegate to the base class implementation.
      return base.Delete(index, throwOnError);
    }
  }
}
