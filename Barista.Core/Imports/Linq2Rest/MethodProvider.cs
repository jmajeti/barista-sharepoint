namespace Barista.Imports.Linq2Rest
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Linq.Expressions;
  using System.Reflection;

  internal static class MethodProvider
  {
    private static readonly ConstantExpression InnerIgnoreCaseExpression;
    private static readonly MethodInfo InnerContainsMethod;
    private static readonly MethodInfo InnerIndexOfMethod;
    private static readonly MethodInfo EndsWithMethod1;
    private static readonly MethodInfo InnerStartsWithMethod;
    private static readonly PropertyInfo InnerLengthProperty;
    private static readonly MethodInfo InnerSubstringMethod;
    private static readonly MethodInfo InnerToLowerMethod;
    private static readonly MethodInfo InnerToUpperMethod;
    private static readonly MethodInfo InnerTrimMethod;
    private static readonly PropertyInfo InnerDayProperty;
    private static readonly PropertyInfo InnerHourProperty;
    private static readonly PropertyInfo InnerMinuteProperty;
    private static readonly PropertyInfo InnerSecondProperty;
    private static readonly PropertyInfo InnerMonthProperty;
    private static readonly PropertyInfo InnerYearProperty;
    private static readonly MethodInfo InnerDoubleRoundMethod;
    private static readonly MethodInfo InnerDecimalRoundMethod;
    private static readonly MethodInfo InnerDoubleFloorMethod;
    private static readonly MethodInfo InnerDecimalFloorMethod;
    private static readonly MethodInfo InnerDoubleCeilingMethod;
    private static readonly MethodInfo InnerDecimalCeilingMethod;

    static MethodProvider()
    {
      var stringType = typeof (string);
      var datetimeType = typeof (DateTime);
      var mathType = typeof (Math);
      var stringComparisonType = typeof (StringComparison);

      InnerIgnoreCaseExpression = Expression.Constant(StringComparison.OrdinalIgnoreCase);

      InnerContainsMethod = stringType.GetMethod("Contains", new[] {stringType});
      InnerIndexOfMethod = stringType.GetMethod("IndexOf", new[] {stringType, stringComparisonType});
      EndsWithMethod1 = stringType.GetMethod("EndsWith", new[] {stringType, stringComparisonType});
      InnerStartsWithMethod = stringType.GetMethod("StartsWith", new[] {stringType, stringComparisonType});
      InnerLengthProperty = stringType.GetProperty("Length", Type.EmptyTypes);
      InnerSubstringMethod = stringType.GetMethod("Substring", new[] {typeof (int)});
      InnerToLowerMethod = stringType.GetMethod("ToLowerInvariant", Type.EmptyTypes);
      InnerToUpperMethod = stringType.GetMethod("ToUpperInvariant", Type.EmptyTypes);
      InnerTrimMethod = stringType.GetMethod("Trim", Type.EmptyTypes);

      InnerDayProperty = datetimeType.GetProperty("Day", Type.EmptyTypes);
      InnerHourProperty = datetimeType.GetProperty("Hour", Type.EmptyTypes);
      InnerMinuteProperty = datetimeType.GetProperty("Minute", Type.EmptyTypes);
      InnerSecondProperty = datetimeType.GetProperty("Second", Type.EmptyTypes);
      InnerMonthProperty = datetimeType.GetProperty("Month", Type.EmptyTypes);
      InnerYearProperty = datetimeType.GetProperty("Year", Type.EmptyTypes);

      InnerDoubleRoundMethod = mathType.GetMethod("Round", new[] {typeof (double)});
      InnerDecimalRoundMethod = mathType.GetMethod("Round", new[] {typeof (decimal)});
      InnerDoubleFloorMethod = mathType.GetMethod("Floor", new[] {typeof (double)});
      InnerDecimalFloorMethod = mathType.GetMethod("Floor", new[] {typeof (decimal)});
      InnerDoubleCeilingMethod = mathType.GetMethod("Ceiling", new[] {typeof (double)});
      InnerDecimalCeilingMethod = mathType.GetMethod("Ceiling", new[] {typeof (decimal)});
    }

    public static ConstantExpression IgnoreCaseExpression
    {
      get { return InnerIgnoreCaseExpression; }
    }

    public static MethodInfo IndexOfMethod
    {
      get { return InnerIndexOfMethod; }
    }

    public static MethodInfo ContainsMethod
    {
      get { return InnerContainsMethod; }
    }

    public static MethodInfo EndsWithMethod
    {
      get { return EndsWithMethod1; }
    }

    public static MethodInfo StartsWithMethod
    {
      get { return InnerStartsWithMethod; }
    }

    public static PropertyInfo LengthProperty
    {
      get { return InnerLengthProperty; }
    }

    public static MethodInfo SubstringMethod
    {
      get { return InnerSubstringMethod; }
    }

    public static MethodInfo ToLowerMethod
    {
      get { return InnerToLowerMethod; }
    }

    public static MethodInfo ToUpperMethod
    {
      get { return InnerToUpperMethod; }
    }

    public static MethodInfo TrimMethod
    {
      get { return InnerTrimMethod; }
    }

    public static PropertyInfo DayProperty
    {
      get { return InnerDayProperty; }
    }

    public static PropertyInfo HourProperty
    {
      get { return InnerHourProperty; }
    }

    public static PropertyInfo MinuteProperty
    {
      get { return InnerMinuteProperty; }
    }

    public static PropertyInfo SecondProperty
    {
      get { return InnerSecondProperty; }
    }

    public static PropertyInfo MonthProperty
    {
      get { return InnerMonthProperty; }
    }

    public static PropertyInfo YearProperty
    {
      get { return InnerYearProperty; }
    }

    public static MethodInfo DoubleRoundMethod
    {
      get { return InnerDoubleRoundMethod; }
    }

    public static MethodInfo DecimalRoundMethod
    {
      get { return InnerDecimalRoundMethod; }
    }

    public static MethodInfo DoubleFloorMethod
    {
      get { return InnerDoubleFloorMethod; }
    }

    public static MethodInfo DecimalFloorMethod
    {
      get { return InnerDecimalFloorMethod; }
    }

    public static MethodInfo DoubleCeilingMethod
    {
      get { return InnerDoubleCeilingMethod; }
    }

    public static MethodInfo DecimalCeilingMethod
    {
      get { return InnerDecimalCeilingMethod; }
    }

    public static MethodInfo GetAnyAllMethod(string name, Type collectionType)
    {
      if (collectionType == null)
        throw new ArgumentNullException("collectionType");

      var implementationType = GetIEnumerableImpl(collectionType);

      var elemType = implementationType.GetGenericArguments()[0];
      var predType = typeof (Func<,>).MakeGenericType(elemType, typeof (bool));

      var allMethod = (MethodInfo) GetGenericMethod(
        typeof (Enumerable),
        name,
        new[] {elemType},
        new[] {implementationType, predType},
        BindingFlags.Static);

      return allMethod;
    }

    public static Type GetIEnumerableImpl(Type type)
    {
      if (type == null)
        throw new ArgumentNullException("type");

      // Get IEnumerable implementation. Either type is IEnumerable<T> for some T, 
      // or it implements IEnumerable<T> for some T. We need to find the interface.
      if (IsIEnumerable(type))
      {
        return type;
      }

      var interfaces = type.FindInterfaces((m, o) => IsIEnumerable(m), null);

      var t = interfaces.First();

      return t;
    }

    private static MethodBase GetGenericMethod(Type type, string name, Type[] typeArgs, Type[] argTypes,
                                               BindingFlags flags)
    {
      if (typeArgs == null)
        throw new ArgumentNullException("typeArgs");

      if (type == null)
        throw new ArgumentNullException("type");

      var typeArity = typeArgs.Length;
      var methods = type.GetMethods()
                        .Where(m => m.Name == name)
                        .Where(m => m.GetGenericArguments().Length == typeArity)
                        .Select(m => m.MakeGenericMethod(typeArgs));

      return Type.DefaultBinder.SelectMethod(flags, methods.ToArray(), argTypes, null);
    }

    private static bool IsIEnumerable(Type type)
    {
      if (type == null)
        throw new ArgumentNullException("type");

      return type.IsGenericType
             && type.GetGenericTypeDefinition() == typeof (IEnumerable<>);
    }
  }
}