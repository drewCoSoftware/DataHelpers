// Clanker code, slightly modified.
using System.Globalization;
using System.Linq.Expressions;
using System.Text;


// ==============================================================================================================================
public static class WhereBuilder
{
  public static string ToSqlWhere<T>(Expression<Func<T, bool>> predicate)
  {
    if (predicate is null) throw new ArgumentNullException(nameof(predicate));
    var sb = new StringBuilder();
    AppendExpression(sb, predicate.Body, parentPrec: 0);
    return sb.ToString();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // Precedence: higher means binds tighter
  // OR = 10, AND = 20, NOT = 30, Comparisons (=, <, >, etc.) = 40, atoms = 100
  private static int GetNodePrecedence(Expression e)
  {
    if (e is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
      return GetNodePrecedence(ue.Operand);

    return e.NodeType switch
    {
      ExpressionType.OrElse => 10,
      ExpressionType.AndAlso => 20,
      ExpressionType.Not => 30,
      ExpressionType.Equal or ExpressionType.NotEqual or
      ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or
      ExpressionType.LessThan or ExpressionType.LessThanOrEqual => 40,
      _ => 100 // member, constant, etc.
    };
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private static void AppendExpression(StringBuilder sb, Expression expr, int parentPrec)
  {
    switch (expr.NodeType)
    {
      case ExpressionType.AndAlso:
      case ExpressionType.OrElse:
      case ExpressionType.Equal:
      case ExpressionType.NotEqual:
      case ExpressionType.GreaterThan:
      case ExpressionType.GreaterThanOrEqual:
      case ExpressionType.LessThan:
      case ExpressionType.LessThanOrEqual:
        AppendBinary(sb, (BinaryExpression)expr, parentPrec);
        break;

      case ExpressionType.MemberAccess:
        AppendMember(sb, (MemberExpression)expr);
        break;

      case ExpressionType.Constant:
        AppendConstant(sb, (ConstantExpression)expr);
        break;

      case ExpressionType.Convert:
        AppendExpression(sb, ((UnaryExpression)expr).Operand, parentPrec);
        break;

      case ExpressionType.Not:
        {
          var opPrec = 30;
          var operand = ((UnaryExpression)expr).Operand;

          // !x.IsActive => @IsActive = 0
          if (TryGetBooleanPropertyAccess(operand, out var name))
          {
            sb.Append('@').Append(name).Append(" = 0");
            break;
          }

          int childPrec = GetNodePrecedence(operand);
          sb.Append("NOT ");
          if (childPrec < opPrec)
          {
            sb.Append('(');
            AppendExpression(sb, operand, parentPrec: 0);
            sb.Append(')');
          }
          else
          {
            AppendExpression(sb, operand, parentPrec: opPrec);
          }
          break;
        }

      case ExpressionType.Parameter:
        sb.Append("1=1"); // shouldn’t really happen in a WHERE; safe default
        break;

      default:
        throw new NotSupportedException($"Unsupported expression node: {expr.NodeType}");
    }
  }

  private static void AppendBinary(StringBuilder sb, BinaryExpression be, int parentPrec)
  {
    int opPrec = GetNodePrecedence(be);

    // Special-case NULL: x.Prop == null / x.Prop != null
    if ((be.NodeType == ExpressionType.Equal || be.NodeType == ExpressionType.NotEqual) &&
        ((IsNullConstant(be.Right) && IsPropertyAccess(be.Left)) ||
         (IsNullConstant(be.Left) && IsPropertyAccess(be.Right))))
    {
      var propSide = IsPropertyAccess(be.Left) ? be.Left : be.Right;
      AppendExpression(sb, propSide, opPrec); // prop is atomic; no parens needed
      sb.Append(be.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
      return;
    }

    // Left side (with parens only if needed)
    var l = be.Left;
    int lp = GetNodePrecedence(l);
    bool lpNeedsParens = lp < opPrec;
    if (lpNeedsParens) sb.Append('(');
    AppendExpression(sb, l, opPrec);
    if (lpNeedsParens) sb.Append(')');

    // Operator
    sb.Append(' ').Append(BinaryOperatorToSql(be.NodeType)).Append(' ');

    // Right side (with parens only if needed)
    var r = be.Right;
    int rp = GetNodePrecedence(r);
    bool rpNeedsParens = rp < opPrec;
    if (rpNeedsParens) sb.Append('(');
    AppendExpression(sb, r, opPrec);
    if (rpNeedsParens) sb.Append(')');
  }

  private static string BinaryOperatorToSql(ExpressionType type) => type switch
  {
    ExpressionType.Equal => "=",
    ExpressionType.NotEqual => "<>",
    ExpressionType.GreaterThan => ">",
    ExpressionType.GreaterThanOrEqual => ">=",
    ExpressionType.LessThan => "<",
    ExpressionType.LessThanOrEqual => "<=",
    ExpressionType.AndAlso => "AND",
    ExpressionType.OrElse => "OR",
    _ => throw new NotSupportedException($"Unsupported binary operator: {type}")
  };

  private static void AppendMember(StringBuilder sb, MemberExpression me)
  {
    if (me.Expression is ParameterExpression)
    {
      // Bare bool / bool? => @Prop = 1
      if (IsBooleanType(me.Type))
        sb.Append('@').Append(me.Member.Name).Append(" = 1");
      else
        sb.Append('@').Append(me.Member.Name);
      return;
    }

    // Captured value or static member
    object? value = TryEvaluateMemberAccess(me);
    AppendValue(sb, value);
  }

  private static void AppendConstant(StringBuilder sb, ConstantExpression ce) => AppendValue(sb, ce.Value);

  private static void AppendValue(StringBuilder sb, object? value)
  {
    if (value is null) { sb.Append("NULL"); return; }

    switch (Type.GetTypeCode(value.GetType()))
    {
      case TypeCode.Boolean: sb.Append((bool)value ? "1" : "0"); break;
      case TypeCode.String:
        sb.Append('\'').Append(((string)value).Replace("'", "''")).Append('\''); break;
      case TypeCode.DateTime:
        sb.Append('\'')
          .Append(((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture))
          .Append('\''); break;
      case TypeCode.Decimal:
      case TypeCode.Double:
      case TypeCode.Single:
      case TypeCode.Byte:
      case TypeCode.SByte:
      case TypeCode.Int16:
      case TypeCode.Int32:
      case TypeCode.Int64:
      case TypeCode.UInt16:
      case TypeCode.UInt32:
      case TypeCode.UInt64:
        sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
      default:
        sb.Append('\'')
          .Append(Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("'", "''"))
          .Append('\''); break;
    }
  }

  private static bool IsNullConstant(Expression e) => e is ConstantExpression ce && ce.Value is null;
  private static bool IsPropertyAccess(Expression e) => e is MemberExpression me && me.Expression is ParameterExpression;
  private static bool IsBooleanType(Type t) => t == typeof(bool) || t == typeof(bool?);

  private static bool TryGetBooleanPropertyAccess(Expression e, out string name)
  {
    if (e is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
      return TryGetBooleanPropertyAccess(ue.Operand, out name);

    if (e is MemberExpression me && me.Expression is ParameterExpression && IsBooleanType(me.Type))
    {
      name = me.Member.Name;
      return true;
    }

    name = default!;
    return false;
  }

  private static object? TryEvaluateMemberAccess(MemberExpression me)
  {
    var objectMember = Expression.Convert(me, typeof(object));
    var getterLambda = Expression.Lambda<Func<object>>(objectMember);
    return getterLambda.Compile().Invoke();
  }
}
