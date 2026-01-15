using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ling.Mapper
{
    /// <summary>
    /// 嵌套属性访问辅助类，支持多级属性路径（如 "User.Profile.Name"）
    /// </summary>
    internal static class NestedPropertyHelper
    {
        /// <summary>
        /// 从对象获取嵌套属性值
        /// </summary>
        /// <param name="obj">源对象</param>
        /// <param name="propertyPath">属性路径（如 "User.Profile.Name"）</param>
        /// <returns>属性值，如果路径中任何对象为 null 则返回 null</returns>
        public static object? GetNestedPropertyValue(object? obj, string propertyPath)
        {
            if (obj == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var properties = propertyPath.Split('.');
            object? current = obj;

            foreach (var propName in properties)
            {
                if (current == null)
                    return null;

                var propInfo = current.GetType().GetProperty(propName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (propInfo == null || !propInfo.CanRead)
                    return null;

                current = propInfo.GetValue(current);
            }

            return current;
        }

        /// <summary>
        /// 设置对象的嵌套属性值（会自动创建中间对象）
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="propertyPath">属性路径（如 "User.Profile.Name"）</param>
        /// <param name="value">要设置的值</param>
        /// <returns>是否设置成功</returns>
        public static bool SetNestedPropertyValue(object? obj, string propertyPath, object? value)
        {
            if (obj == null || string.IsNullOrEmpty(propertyPath))
                return false;

            var properties = propertyPath.Split('.');
            if (properties.Length == 0)
                return false;

            // 导航到最后一级的父对象
            object? current = obj;
            for (int i = 0; i < properties.Length - 1; i++)
            {
                var propName = properties[i];
                var propInfo = current.GetType().GetProperty(propName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (propInfo == null || !propInfo.CanRead)
                    return false;

                var nextValue = propInfo.GetValue(current);

                // 如果中间对象为 null，尝试创建
                if (nextValue == null && propInfo.CanWrite)
                {
                    if (!propInfo.PropertyType.IsValueType &&
                        propInfo.PropertyType.GetConstructor(Type.EmptyTypes) != null)
                    {
                        try
                        {
                            nextValue = Activator.CreateInstance(propInfo.PropertyType);
                            propInfo.SetValue(current, nextValue);
                        }
                        catch
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                current = nextValue;
                if (current == null)
                    return false;
            }

            // 设置最后一级属性
            var lastPropName = properties[properties.Length - 1];
            var lastPropInfo = current.GetType().GetProperty(lastPropName,
                BindingFlags.Public | BindingFlags.Instance);

            if (lastPropInfo == null || !lastPropInfo.CanWrite)
                return false;

            try
            {
                lastPropInfo.SetValue(current, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取嵌套属性的 PropertyInfo
        /// </summary>
        /// <param name="type">起始类型</param>
        /// <param name="propertyPath">属性路径</param>
        /// <returns>最终属性的 PropertyInfo，如果路径无效返回 null</returns>
        public static PropertyInfo? GetNestedPropertyInfo(Type type, string propertyPath)
        {
            if (type == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var properties = propertyPath.Split('.');
            Type currentType = type;
            PropertyInfo? currentProp = null;

            foreach (var propName in properties)
            {
                currentProp = currentType.GetProperty(propName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (currentProp == null)
                    return null;

                currentType = currentProp.PropertyType;
            }

            return currentProp;
        }

        /// <summary>
        /// 检查属性路径是否有效
        /// </summary>
        /// <param name="type">起始类型</param>
        /// <param name="propertyPath">属性路径</param>
        /// <returns>是否有效</returns>
        public static bool IsValidPropertyPath(Type type, string propertyPath)
        {
            return GetNestedPropertyInfo(type, propertyPath) != null;
        }
    }
}
