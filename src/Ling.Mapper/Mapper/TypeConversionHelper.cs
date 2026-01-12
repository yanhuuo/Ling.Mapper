using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Ling.Mapper
{
    /// <summary>
    /// 类型转换辅助类，负责将 ConvertSimpleType 拆分为更小的、可维护的方法。
    /// 每个方法专注于特定的转换场景，提高代码可读性和 JIT 优化效果。
    /// </summary>
    internal static class TypeConversionHelper
    {
        /// <summary>
        /// 处理枚举类型的转换（enum ? int, enum ? string, enum ? enum）
        /// </summary>
        public static Expression? TryConvertEnum(
            Expression srcAccess,
            Type srcType,
            Type destType,
            Type srcUnderlyingType,
            Type destUnderlyingType,
            bool srcIsNullable,
            bool destIsNullable)
        {
            // enum -> int
            if (srcUnderlyingType.IsEnum && destUnderlyingType == typeof(int))
            {
                return ConvertEnumToInt(srcAccess, srcType, destType, srcIsNullable, destIsNullable);
            }

            // int -> enum
            if (srcUnderlyingType == typeof(int) && destUnderlyingType.IsEnum)
            {
                return ConvertIntToEnum(srcAccess, srcType, destType, destUnderlyingType, srcIsNullable, destIsNullable);
            }

            // enum -> string
            if (srcUnderlyingType.IsEnum && destUnderlyingType == typeof(string))
            {
                return ConvertEnumToString(srcAccess, srcType, srcUnderlyingType, srcIsNullable);
            }

            // string -> enum
            if (srcUnderlyingType == typeof(string) && destUnderlyingType.IsEnum)
            {
                return ConvertStringToEnum(srcAccess, destType, destUnderlyingType, destIsNullable);
            }

            // enum -> enum (不同类型)
            if (srcUnderlyingType.IsEnum && destUnderlyingType.IsEnum && srcUnderlyingType != destUnderlyingType)
            {
                return ConvertEnumToEnum(srcAccess, srcType, destType, destUnderlyingType, srcIsNullable, destIsNullable);
            }

            return null;
        }

        /// <summary>
        /// 处理可空类型的转换（T ? T?, T? ? U, etc.）
        /// </summary>
        public static Expression? TryConvertNullable(
            Expression srcAccess,
            Type srcType,
            Type destType,
            Type srcUnderlyingType,
            Type destUnderlyingType,
            bool srcIsNullable,
            bool destIsNullable)
        {
            // T -> T? (非可空到可空，底层类型相同)
            if (!srcIsNullable && destIsNullable && srcUnderlyingType == destUnderlyingType)
            {
                return Expression.Convert(srcAccess, destType);
            }

            // T? -> T (可空到非可空，底层类型相同)
            if (srcIsNullable && !destIsNullable && srcUnderlyingType == destUnderlyingType)
            {
                return ConvertNullableToNonNullable(srcAccess, srcType, destType);
            }

            // T? -> U? (可空到可空，底层类型不同)
            if (srcIsNullable && destIsNullable)
            {
                return ConvertNullableToNullable(srcAccess, srcType, destType, destUnderlyingType);
            }

            // T -> U? (非可空到不同类型的可空)
            if (!srcIsNullable && destIsNullable && srcUnderlyingType != destUnderlyingType)
            {
                var converted = Expression.Convert(srcAccess, destUnderlyingType);
                return Expression.Convert(converted, destType);
            }

            // T? -> U (可空到不同类型的非可空)
            if (srcIsNullable && !destIsNullable && srcUnderlyingType != destUnderlyingType)
            {
                var getValueMethod = srcType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
                if (getValueMethod != null)
                {
                    var value = Expression.Call(srcAccess, getValueMethod);
                    return Expression.Convert(value, destType);
                }
            }

            return null;
        }

        /// <summary>
        /// 处理简单类型之间的直接转换
        /// </summary>
        public static Expression ConvertSimpleCast(
            Expression srcAccess,
            Type destType)
        {
            return Expression.Convert(srcAccess, destType);
        }

        #region Enum Conversion Helpers

        private static Expression ConvertEnumToInt(
            Expression srcAccess,
            Type srcType,
            Type destType,
            bool srcIsNullable,
            bool destIsNullable)
        {
            if (srcIsNullable && !destIsNullable)
            {
                // enum? -> int
                var getValueMethod = srcType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
                if (getValueMethod != null)
                {
                    var nullableValue = Expression.Call(srcAccess, getValueMethod);
                    return Expression.Convert(nullableValue, typeof(int));
                }
            }
            else if (srcIsNullable && destIsNullable)
            {
                // enum? -> int?
                var hasValueProp = srcType.GetProperty("HasValue");
                var valueProp = srcType.GetProperty("Value");
                if (hasValueProp != null && valueProp != null)
                {
                    return Expression.Condition(
                        Expression.Property(srcAccess, hasValueProp),
                        Expression.Convert(Expression.Convert(Expression.Property(srcAccess, valueProp), typeof(int)), typeof(int?)),
                        Expression.Default(typeof(int?))
                    );
                }
            }
            else if (!srcIsNullable && destIsNullable)
            {
                // enum -> int?
                return Expression.Convert(Expression.Convert(srcAccess, typeof(int)), typeof(int?));
            }
            else
            {
                // enum -> int
                return Expression.Convert(srcAccess, typeof(int));
            }

            return Expression.Convert(srcAccess, destType);
        }

        private static Expression ConvertIntToEnum(
            Expression srcAccess,
            Type srcType,
            Type destType,
            Type destUnderlyingType,
            bool srcIsNullable,
            bool destIsNullable)
        {
            if (srcIsNullable && !destIsNullable)
            {
                // int? -> enum
                var getValueMethod = srcType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
                if (getValueMethod != null)
                {
                    var intValue = Expression.Call(srcAccess, getValueMethod);
                    return Expression.Convert(intValue, destUnderlyingType);
                }
            }
            else if (srcIsNullable && destIsNullable)
            {
                // int? -> enum?
                var hasValueProp = srcType.GetProperty("HasValue");
                var valueProp = srcType.GetProperty("Value");
                if (hasValueProp != null && valueProp != null)
                {
                    return Expression.Condition(
                        Expression.Property(srcAccess, hasValueProp),
                        Expression.Convert(Expression.Convert(Expression.Property(srcAccess, valueProp), destUnderlyingType), destType),
                        Expression.Default(destType)
                    );
                }
            }
            else if (!srcIsNullable && destIsNullable)
            {
                // int -> enum?
                return Expression.Convert(Expression.Convert(srcAccess, destUnderlyingType), destType);
            }
            else
            {
                // int -> enum
                return Expression.Convert(srcAccess, destUnderlyingType);
            }

            return Expression.Convert(srcAccess, destType);
        }

        private static Expression ConvertEnumToString(
            Expression srcAccess,
            Type srcType,
            Type srcUnderlyingType,
            bool srcIsNullable)
        {
            var toStringMethod = srcUnderlyingType.GetMethod("ToString", Type.EmptyTypes);
            if (toStringMethod != null)
            {
                if (srcIsNullable)
                {
                    // enum? -> string
                    var hasValueProp = srcType.GetProperty("HasValue");
                    var valueProp = srcType.GetProperty("Value");
                    if (hasValueProp != null && valueProp != null)
                    {
                        return Expression.Condition(
                            Expression.Property(srcAccess, hasValueProp),
                            Expression.Call(Expression.Property(srcAccess, valueProp), toStringMethod),
                            Expression.Constant(null, typeof(string))
                        );
                    }
                }
                else
                {
                    // enum -> string
                    return Expression.Call(srcAccess, toStringMethod);
                }
            }

            return Expression.Constant(null, typeof(string));
        }

        private static Expression ConvertStringToEnum(
            Expression srcAccess,
            Type destType,
            Type destUnderlyingType,
            bool destIsNullable)
        {
            var enumParseMethod = typeof(Enum).GetMethod("Parse", new[] { typeof(Type), typeof(string), typeof(bool) });
            if (enumParseMethod != null)
            {
                if (destIsNullable)
                {
                    // string -> enum?
                    var isNullOrEmptyMethod = typeof(string).GetMethod("IsNullOrEmpty", new[] { typeof(string) });
                    if (isNullOrEmptyMethod != null)
                    {
                        var parseExpr = Expression.Call(
                            enumParseMethod,
                            Expression.Constant(destUnderlyingType, typeof(Type)),
                            srcAccess,
                            Expression.Constant(true)
                        );

                        return Expression.Condition(
                            Expression.Call(isNullOrEmptyMethod, srcAccess),
                            Expression.Default(destType),
                            Expression.Convert(Expression.Convert(parseExpr, destUnderlyingType), destType)
                        );
                    }
                }
                else
                {
                    // string -> enum
                    var parseExpr = Expression.Call(
                        enumParseMethod,
                        Expression.Constant(destUnderlyingType, typeof(Type)),
                        srcAccess,
                        Expression.Constant(true)
                    );
                    return Expression.Convert(parseExpr, destUnderlyingType);
                }
            }

            return Expression.Default(destType);
        }

        private static Expression ConvertEnumToEnum(
            Expression srcAccess,
            Type srcType,
            Type destType,
            Type destUnderlyingType,
            bool srcIsNullable,
            bool destIsNullable)
        {
            if (srcIsNullable && !destIsNullable)
            {
                // enum? -> enum (不同类型)
                var getValueMethod = srcType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
                if (getValueMethod != null)
                {
                    var enumValue = Expression.Call(srcAccess, getValueMethod);
                    var intValue = Expression.Convert(enumValue, typeof(int));
                    return Expression.Convert(intValue, destUnderlyingType);
                }
            }
            else if (srcIsNullable && destIsNullable)
            {
                // enum? -> enum? (不同类型)
                var hasValueProp = srcType.GetProperty("HasValue");
                var valueProp = srcType.GetProperty("Value");
                if (hasValueProp != null && valueProp != null)
                {
                    var intValue = Expression.Convert(Expression.Property(srcAccess, valueProp), typeof(int));
                    var destEnumValue = Expression.Convert(intValue, destUnderlyingType);
                    return Expression.Condition(
                        Expression.Property(srcAccess, hasValueProp),
                        Expression.Convert(destEnumValue, destType),
                        Expression.Default(destType)
                    );
                }
            }
            else if (!srcIsNullable && destIsNullable)
            {
                // enum -> enum? (不同类型)
                var intValue = Expression.Convert(srcAccess, typeof(int));
                var destEnumValue = Expression.Convert(intValue, destUnderlyingType);
                return Expression.Convert(destEnumValue, destType);
            }
            else
            {
                // enum -> enum (不同类型)
                var intValue = Expression.Convert(srcAccess, typeof(int));
                return Expression.Convert(intValue, destUnderlyingType);
            }

            return Expression.Convert(srcAccess, destType);
        }

        #endregion

        #region Nullable Conversion Helpers

        private static Expression ConvertNullableToNonNullable(
            Expression srcAccess,
            Type srcType,
            Type destType)
        {
            // T? -> T: 使用 GetValueOrDefault()
            var getValueMethod = srcType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
            if (getValueMethod != null)
            {
                return Expression.Call(srcAccess, getValueMethod);
            }

            // 备用方案：条件表达式
            var hasValueProp = srcType.GetProperty("HasValue");
            var valueProp = srcType.GetProperty("Value");
            if (hasValueProp != null && valueProp != null)
            {
                return Expression.Condition(
                    Expression.Property(srcAccess, hasValueProp),
                    Expression.Property(srcAccess, valueProp),
                    Expression.Default(destType)
                );
            }

            return Expression.Default(destType);
        }

        private static Expression ConvertNullableToNullable(
            Expression srcAccess,
            Type srcType,
            Type destType,
            Type destUnderlyingType)
        {
            // T? -> U?: 先转换底层类型，再包装为可空
            var hasValueProp = srcType.GetProperty("HasValue");
            var valueProp = srcType.GetProperty("Value");

            if (hasValueProp != null && valueProp != null)
            {
                var convertedValue = Expression.Convert(
                    Expression.Property(srcAccess, valueProp),
                    destUnderlyingType
                );

                return Expression.Condition(
                    Expression.Property(srcAccess, hasValueProp),
                    Expression.Convert(convertedValue, destType),
                    Expression.Default(destType)
                );
            }

            return Expression.Default(destType);
        }

        #endregion
    }
}
