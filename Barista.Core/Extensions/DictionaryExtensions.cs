﻿namespace Barista.Extensions
{
  using System.Collections.Generic;

  /// <summary>
  /// Extension class for IDictionary
  /// </summary>
  public static class DictionaryExtensions
  {
    /// <summary>
    /// Gets the value by key.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns></returns>
    public static T GetValue<T>(this IDictionary<object, object> dictionary, object key)
        where T : new()
    {
      T defaultValue = default(T);
      return GetValue(dictionary, key, defaultValue);
    }

    /// <summary>
    /// Gets the value by key and default value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns></returns>
    public static T GetValue<T>(this IDictionary<object, object> dictionary, object key, T defaultValue)
    {
      object valueObj;

      if (!dictionary.TryGetValue(key, out valueObj))
      {
        return defaultValue;
      }

      return (T)valueObj;
    }
  }
}