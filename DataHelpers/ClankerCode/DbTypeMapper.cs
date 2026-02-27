using DataHelpers;
using drewCo.Tools;
using System;
using System.Data;
using System.Reflection;

namespace ClankerCode;

// ==============================================================================================================================
public interface IDbTypeMapper
{
  // --------------------------------------------------------------------------------------------------------------------------
  public DbType ToDbType(Type type)
  {
    if (type == null)
    {
      throw new ArgumentNullException(nameof(type));
    }

    Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

    // Single Relations are represented as integers b/c of ID.
    if (ReflectionTools.HasInterface<ISingleRelation>(underlyingType)) { 
      return DbType.Int32;
    }

    if (underlyingType.IsEnum)
    {
      underlyingType = Enum.GetUnderlyingType(underlyingType);
    }

    if (underlyingType == typeof(string))
    {
      return DbType.String;
    }

    if (underlyingType == typeof(char))
    {
      return DbType.StringFixedLength;
    }

    if (underlyingType == typeof(bool))
    {
      return DbType.Boolean;
    }

    if (underlyingType == typeof(byte))
    {
      return DbType.Byte;
    }

    if (underlyingType == typeof(sbyte))
    {
      return DbType.SByte;
    }

    if (underlyingType == typeof(short))
    {
      return DbType.Int16;
    }

    if (underlyingType == typeof(ushort))
    {
      return DbType.UInt16;
    }

    if (underlyingType == typeof(int))
    {
      return DbType.Int32;
    }

    if (underlyingType == typeof(uint))
    {
      return DbType.UInt32;
    }

    if (underlyingType == typeof(long))
    {
      return DbType.Int64;
    }

    if (underlyingType == typeof(ulong))
    {
      return DbType.UInt64;
    }

    if (underlyingType == typeof(float))
    {
      return DbType.Single;
    }

    if (underlyingType == typeof(double))
    {
      return DbType.Double;
    }

    if (underlyingType == typeof(decimal))
    {
      return DbType.Decimal;
    }

    if (underlyingType == typeof(DateTime))
    {
      return DbType.DateTime2;
    }

    if (underlyingType == typeof(DateTimeOffset))
    {
      return DbType.DateTimeOffset;
    }

    if (underlyingType == typeof(TimeSpan))
    {
      return DbType.Time;
    }

    if (underlyingType == typeof(Guid))
    {
      return DbType.Guid;
    }

    if (underlyingType == typeof(byte[]))
    {
      return DbType.Binary;
    }

    if (underlyingType == typeof(object))
    {
      return DbType.Object;
    }

    throw new InvalidOperationException($"The type: {underlyingType} is not supported!");
  }
}
