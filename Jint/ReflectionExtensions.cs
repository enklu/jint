#if (NETFX_CORE || NETSTANDARD1_3 || NETSTANDARD2_0)
using System;
using System.Linq;
using System.Reflection;

public static class ReflectionExtensions
{
    public static bool IsEnum(this Type type)
    {
        return type.GetTypeInfo().IsEnum;
    }

    public static bool IsGenericType(this Type type)
    {
        return type.GetTypeInfo().IsGenericType;
    }

    public static bool IsValueType(this Type type)
    {
        return type.GetTypeInfo().IsValueType;
    }

    public static bool HasAttribute<T>(this ParameterInfo member) where T : Attribute
    {
        return member.GetCustomAttributes<T>().Any();
    }

    public static bool HasAttribute<T>(this MethodBase methodBase) where T : Attribute
    {
        return methodBase.GetCustomAttributes<T>().Any();
    }

    public static bool HasAttribute(this ParameterInfo member, Type attributeType)
    {
        return member.GetCustomAttributes(attributeType, true).Any();
    }

    public static bool HasAttribute(this MethodBase methodBase, Type attributeType)
    {
        return methodBase.GetCustomAttributes(attributeType, true).Any();
    }

    public static T[] GetCustomAttributes<T>(this Type @this, bool inherit) where T : Attribute
    {
        return (T[]) @this.GetTypeInfo().GetCustomAttributes(typeof(T), inherit).ToArray();
    }

    public static object[] GetCustomAttributes(this Type @this, Type attributeType, bool inherit)
    {
        return @this.GetTypeInfo().GetCustomAttributes(attributeType, inherit).ToArray();
    }
}
#else
using System;
using System.Reflection;

public static class ReflectionExtensions
{
    public static bool IsEnum(this Type type)
    {
        return type.IsEnum;
    }

    public static bool IsGenericType(this Type type)
    {
        return type.IsGenericType;
    }

    public static bool IsValueType(this Type type)
    {
        return type.IsValueType;
    }

    public static bool HasAttribute<T>(this ParameterInfo member) where T : Attribute
    {
        return Attribute.IsDefined(member, typeof(T));
    }

    public static bool HasAttribute<T>(this MethodBase methodBase) where T : Attribute
    {
        return Attribute.IsDefined(methodBase, typeof(T), true);
    }

    public static bool HasAttribute(this ParameterInfo member, Type attributeType)
    {
        return Attribute.IsDefined(member, attributeType);
    }

    public static bool HasAttribute(this MethodBase methodBase, Type attributeType)
    {
        return Attribute.IsDefined(methodBase, attributeType, true);
    }

    public static T[] GetCustomAttributes<T>(this Type @this, bool inherit) where T : Attribute
    {
        return (T[])Attribute.GetCustomAttributes(@this, typeof(T), inherit);
    }

    public static object[] GetCustomAttributes(this Type @this, Type attributeType, bool inherit)
    {
        return (object[]) Attribute.GetCustomAttributes(@this, attributeType, inherit);
    }

    public static MethodInfo GetMethodInfo(this Delegate d)
    {
        return d.Method;
    }
}
#endif
