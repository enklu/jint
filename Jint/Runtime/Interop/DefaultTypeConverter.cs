using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using Jint.Native;
using System.Collections.Generic;
using System.Reflection;

namespace Jint.Runtime.Interop
{
    public class DefaultTypeConverter : ITypeConverter
    {
        private readonly Engine _engine;
        private static readonly Dictionary<string, bool> _knownConversions = new Dictionary<string, bool>();
        private static readonly object _lockObject = new object();

        private readonly Dictionary<Delegate, object> _delegateCache = new Dictionary<Delegate, object>();
        private readonly Dictionary<Type, ICallableConversion> _delegateConversions = new Dictionary<Type, ICallableConversion>();

        private static MethodInfo convertChangeType = typeof(System.Convert).GetMethod("ChangeType", new Type[] { typeof(object), typeof(Type), typeof(IFormatProvider) });
        private static MethodInfo jsValueFromObject = typeof(JsValue).GetMethod("FromObject");
        private static MethodInfo jsValueToObject = typeof(JsValue).GetMethod("ToObject");
        private static Expression JsUndefExpr = Expression.Constant(JsValue.Undefined, typeof(JsValue));

        public DefaultTypeConverter(Engine engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// Registers a conversion operation for a specific target type. When the callable must be adapted to this type,
        /// use the provided conversion operation.
        /// </summary>
        /// <param name="targetType">The <see cref="Type"/> of the object Jint is trying to convert the callable to.</param>
        /// <param name="conversion">The conversion implementation to use for the specific target type</param>
        public void RegisterDelegateConversion(Type targetType, ICallableConversion conversion)
        {
            if (_delegateConversions.ContainsKey(targetType))
            {
                return;
            }

            _delegateConversions[targetType] = conversion;
        }

        public virtual object Convert(object value, Type type, IFormatProvider formatProvider)
        {
            if (value == null)
            {
                if (TypeConverter.TypeIsNullable(type))
                {
                    return null;
                }

                throw new NotSupportedException(string.Format("Unable to convert null to '{0}'", type.FullName));
            }

            // don't try to convert if value is derived from type
            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            if (type.IsEnum())
            {
                var integer = System.Convert.ChangeType(value, typeof(int), formatProvider);
                if (integer == null)
                {
                    throw new ArgumentOutOfRangeException();
                }

                return Enum.ToObject(type, integer);
            }

            var valueType = value.GetType();
            // is the javascript value an ICallable instance ?
            if (valueType == typeof(Func<JsValue, JsValue[], JsValue>))
            {
                var function = (Func<JsValue, JsValue[], JsValue>)value;

                // Check Cache for existing conversion
                if (_delegateCache.ContainsKey(function))
                {
                    return _delegateCache[function];
                }

                // Check for registered callable conversion
                if (_delegateConversions.ContainsKey(type))
                {
                    var converted = _delegateConversions[type].Convert(function);
                    return Cache(function, converted);
                }

                if (type.IsGenericType())
                {
                    var genericType = type.GetGenericTypeDefinition();

                    // create the requested Delegate
                    if (genericType.Name.StartsWith("Action"))
                    {
                        return ConvertToGenericAction(type, function);
                    }

                    if (genericType.Name.StartsWith("Func"))
                    {
                        return ConvertToGenericFunc(type, function);
                    }
                }
                else
                {
                    if (type == typeof(Action))
                    {
                        return ConvertToAction(function);
                    }

                    if (typeof(MulticastDelegate).IsAssignableFrom(type))
                    {
                        return ConvertToMulticastDelegate(type, function);
                    }
                }

            }

            if (type.IsArray)
            {
                var source = value as object[];
                if (source == null)
                    throw new ArgumentException(String.Format("Value of object[] type is expected, but actual type is {0}.", value.GetType()));

                var targetElementType = type.GetElementType();
                var itemsConverted = source.Select(o => Convert(o, targetElementType, formatProvider)).ToArray();
                var result = Array.CreateInstance(targetElementType, source.Length);
                itemsConverted.CopyTo(result, 0);
                return result;
            }

            if (type.IsGenericType() && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            return System.Convert.ChangeType(value, type, formatProvider);
        }

        /// <summary>
        /// Converts to a basic action delegate.
        /// </summary>
        private object ConvertToAction(Func<JsValue, JsValue[], JsValue> function)
        {
            return Cache(function, (Action)(() => function(JsValue.Undefined, new JsValue[0])));
        }

        /// <summary>
        /// Converts a generic Action delegate
        /// </summary>
        private object ConvertToGenericAction(Type type, Func<JsValue, JsValue[], JsValue> function)
        {
            var genericArguments = type.GetGenericArguments();
            var @params = ToParameterExpressions(genericArguments);
            var @vars = NewParamsArray(@params);

            var callExpression = Expression.Call(
                Expression.Call(
                    Expression.Constant(function.Target),
                    function.GetMethodInfo(),
                    JsUndefExpr,
                    @vars),
                jsValueToObject);

            return Cache(function, Expression.Lambda(type, callExpression, @params).Compile());
        }

        /// <summary>
        /// Converts to a generic Func delegate
        /// </summary>
        private object ConvertToGenericFunc(Type type, Func<JsValue, JsValue[], JsValue> function)
        {
            var genericArguments = type.GetGenericArguments();
            var returnType = genericArguments[genericArguments.Length - 1];

            Type[] paramArgs = new Type[genericArguments.Length - 1];
            for (var i = 0; i < paramArgs.Length; ++i)
            {
                paramArgs[i] = genericArguments[i];
            }

            var @params = ToParameterExpressions(paramArgs);
            var @vars = NewParamsArray(@params);
            
            // the final result's type needs to be changed before casting,
            // for instance when a function returns a number (double) but C# expects an integer
            var delegateCall = Expression.Call(
                Expression.Call(
                    Expression.Constant(function.Target),
                    function.GetMethodInfo(),
                    JsUndefExpr,
                    @vars),
                jsValueToObject);

            var conversionCall = Expression.Call(
                convertChangeType,
                delegateCall,
                Expression.Constant(returnType, typeof(Type)),
                Expression.Constant(System.Globalization.CultureInfo.InvariantCulture, typeof(IFormatProvider)));

            var callExpression = Expression.Convert(conversionCall, returnType);

            return Cache(function, Expression.Lambda(type, callExpression, new ReadOnlyCollection<ParameterExpression>(@params)).Compile());
        }

        /// <summary>
        /// Converts to a multicast delegate
        /// </summary>
        private object ConvertToMulticastDelegate(Type type, Func<JsValue, JsValue[], JsValue> function)
        {
            var method = type.GetMethod("Invoke");
            var arguments = method.GetParameters();
            var @params = ToParameterExpressions(arguments);
            var @vars = NewParamsArray(@params);

            var callExpression = Expression.Call(
                Expression.Call(
                    Expression.Constant(function.Target),
                    function.GetMethodInfo(),
                    JsUndefExpr,
                    @vars),
                jsValueToObject);

            var dynamicExpression = Expression.Invoke(
                Expression.Lambda(type, callExpression, @params),
                @params.Cast<Expression>());

            return Cache(function, Expression.Lambda(type, dynamicExpression, @params).Compile());
        }

        /// <summary>
        /// Converts types to parameter expressions.
        /// </summary>
        private ParameterExpression[] ToParameterExpressions(Type[] arguments)
        {
            var @params = new ParameterExpression[arguments.Length];
            for (var i = 0; i < @params.Length; i++)
            {
                @params[i] = Expression.Parameter(arguments[i], arguments[i].Name + i);
            }

            return @params;
        }

        /// <summary>
        /// Converts parameter info to parameter expressions.
        /// </summary>
        private ParameterExpression[] ToParameterExpressions(ParameterInfo[] arguments)
        {
            var @params = new ParameterExpression[arguments.Length];
            for (var i = 0; i < @params.Length; i++)
            {
                @params[i] = Expression.Parameter(arguments[i].ParameterType, arguments[i].Name + i);
            }

            return @params;
        }

        /// <summary>
        /// Converts parameter expressions into an array expression.
        /// </summary>
        private NewArrayExpression NewParamsArray(ParameterExpression[] @params)
        {
            var tmpVars = new Expression[@params.Length];
            for (var i = 0; i < @params.Length; i++)
            {
                var param = @params[i];
                if (param.Type.IsValueType())
                {
                    var boxing = Expression.Convert(param, typeof(object));
                    tmpVars[i] = Expression.Call(jsValueFromObject, Expression.Constant(_engine, typeof(Engine)), boxing);
                }
                else
                {
                    tmpVars[i] = Expression.Call(jsValueFromObject, Expression.Constant(_engine, typeof(Engine)), param);
                }
            }

            return Expression.NewArrayInit(typeof(JsValue), tmpVars);
        }


        public virtual bool TryConvert(object value, Type type, IFormatProvider formatProvider, out object converted)
        {
            bool canConvert;
            var key = value == null ? String.Format("Null->{0}", type) : String.Format("{0}->{1}", value.GetType(), type);

            if (!_knownConversions.TryGetValue(key, out canConvert))
            {
                lock (_lockObject)
                {
                    if (!_knownConversions.TryGetValue(key, out canConvert))
                    {
                        try
                        {
                            converted = Convert(value, type, formatProvider);
                            _knownConversions.Add(key, true);
                            return true;
                        }
                        catch
                        {
                            converted = null;
                            _knownConversions.Add(key, false);
                            return false;
                        }
                    }
                }
            }

            if (canConvert)
            {
                try
                {
                    converted = Convert(value, type, formatProvider);
                    return true;
                }
                catch
                {
                    converted = null;
                    return false;
                }
            }

            converted = null;
            return false;
        }

        /// <summary>
        /// Caches the wrapper for a specific callable.
        /// </summary>
        private object Cache(Func<JsValue, JsValue[], JsValue> callable, object target)
        {
            _delegateCache[callable] = target;
            return target;
        }
    }
}
