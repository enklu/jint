using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Memory;

namespace Jint.Runtime.Interop
{
    public sealed class MethodInfoFunctionInstance : FunctionInstance
    {
        private readonly Engine _engine;
        private readonly MethodInfo[] _methods;
        
        public MethodInfoFunctionInstance(Engine engine, MethodInfo[] methods)
            : base(engine, null, null, false)
        {
            _engine = engine;
            _methods = methods;

            Prototype = _engine.Function.PrototypeObject;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Invoke(_methods, thisObject, arguments);
        }

        public JsValue Invoke(MethodInfo[] methodInfos, JsValue thisObject, JsValue[] jsArguments)
        {
            var arguments = ProcessParamsArrays(jsArguments, methodInfos);
            var methods = TypeConverter.FindBestMatch(Engine, methodInfos, arguments).ToList();
            if (0 == methods.Count)
            {
                var argumentList = arguments.ToList();
                argumentList.Insert(0, JsValue.FromObject(_engine, _engine));
                arguments = argumentList.ToArray();

                methods = TypeConverter.FindBestMatch(_engine, methodInfos, arguments).ToList();
            }

            var converter = Engine.ClrTypeConverter;

            // try to inject Engine parameter first
            for (int q = 0, qlen = methods.Count; q < qlen; q++)
            {
                var method = methods[q];
                if (method.HasAttribute<DenyJsAccess>())
                {
                    continue;
                }

                var methodParameters = method.GetParameters();
                if (methodParameters.Length == 0 || methodParameters.Length != arguments.Length + 1)
                {
                    continue;
                }

                var parameters = new object[arguments.Length + 1];
                var argumentsMatch = true;

                var parameterType = methodParameters[0].ParameterType;
                if (parameterType != typeof(Engine))
                {
                    continue;
                }

                parameters[0] = _engine;

                for (var i = 0; i < arguments.Length; i++)
                {
                    parameterType = methodParameters[i + 1].ParameterType;

                    if (parameterType == typeof(JsValue))
                    {
                        parameters[i + 1] = arguments[i];
                    }
                    else if (parameterType == typeof(JsValue[]) && arguments[i].IsArray())
                    {
                        // Handle specific case of F(params JsValue[])

                        var arrayInstance = arguments[i].AsArray();
                        var len = TypeConverter.ToInt32(arrayInstance.Get("length"));
                        var result = new JsValue[len];
                        for (var k = 0; k < len; k++)
                        {
                            var pk = k.ToString();
                            result[k] = arrayInstance.HasProperty(pk)
                                ? arrayInstance.Get(pk)
                                : JsValue.Undefined;
                        }

                        parameters[i + 1] = result;
                    }
                    else
                    {
                        if (!converter.TryConvert(arguments[i].ToObject(), parameterType, CultureInfo.InvariantCulture,
                            out parameters[i + 1]))
                        {
                            argumentsMatch = false;
                            break;
                        }

                        var lambdaExpression = parameters[i] as LambdaExpression;
                        if (lambdaExpression != null)
                        {
                            parameters[i + 1] = lambdaExpression.Compile();
                        }
                    }
                }

                if (!argumentsMatch)
                {
                    continue;
                }

                // todo: cache method info
                try
                {
                    return JsValue.FromObject(Engine, method.Invoke(thisObject.ToObject(), parameters.ToArray()));
                }
                catch (TargetInvocationException exception)
                {
                    var meaningfulException = exception.InnerException ?? exception;
                    var handler = Engine.Options._ClrExceptionsHandler;

                    if (handler != null && handler(meaningfulException))
                    {
                        throw new JavaScriptException(Engine.Error, meaningfulException.Message);
                    }

                    throw meaningfulException;
                }
            }

            // check for exact parameter match (no Engine injection)
            for (int q = 0, qlen = methods.Count; q < qlen; q++)
            {
                var method = methods[q];
                if (method.HasAttribute<DenyJsAccess>())
                {
                    continue;
                }

                var parameters = new object[arguments.Length];
                var argumentsMatch = true;

                for (var i = 0; i < arguments.Length; i++)
                {
                    var parameterType = method.GetParameters()[i].ParameterType;

                    if (parameterType == typeof(JsValue))
                    {
                        parameters[i] = arguments[i];
                    }
                    else if (parameterType == typeof(JsValue[]) && arguments[i].IsArray())
                    {
                        // Handle specific case of F(params JsValue[])

                        var arrayInstance = arguments[i].AsArray();
                        var len = TypeConverter.ToInt32(arrayInstance.Get("length"));
                        var result = new JsValue[len];
                        for (var k = 0; k < len; k++)
                        {
                            var pk = k.ToString();
                            result[k] = arrayInstance.HasProperty(pk)
                                ? arrayInstance.Get(pk)
                                : JsValue.Undefined;
                        }

                        parameters[i] = result;
                    }
                    else
                    {
                        if (!converter.TryConvert(arguments[i].ToObject(), parameterType, CultureInfo.InvariantCulture,
                            out parameters[i]))
                        {
                            argumentsMatch = false;
                            break;
                        }

                        var lambdaExpression = parameters[i] as LambdaExpression;
                        if (lambdaExpression != null)
                        {
                            parameters[i] = lambdaExpression.Compile();
                        }
                    }
                }

                if (!argumentsMatch)
                {
                    continue;
                }

                // todo: cache method info
                try
                {
                    return JsValue.FromObject(Engine, method.Invoke(thisObject.ToObject(), parameters.ToArray()));
                }
                catch (TargetInvocationException exception)
                {
                    var meaningfulException = exception.InnerException ?? exception;
                    var handler = Engine.Options._ClrExceptionsHandler;

                    if (handler != null && handler(meaningfulException))
                    {
                        throw new JavaScriptException(Engine.Error, meaningfulException.Message);
                    }

                    throw meaningfulException;
                }
            }

            throw new JavaScriptException(Engine.TypeError, "No public methods with the specified arguments were found.");
        }

        /// <summary>
        /// Returns true if any of the parameters has 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private bool AnyHasAttribute<T>(ParameterInfo[] parameters) where T : Attribute
        {
            for (int i = 0; i < parameters.Length; ++i)
            {
                if (parameters[i].HasAttribute<T>())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a new JS array containing the provided js values.
        /// </summary>
        private JsValue NewJsArray(JsValue[] values)
        {
            var jsArray = Engine.Array.Construct(Arguments.Empty);
            Engine.Array.PrototypeObject.Push(jsArray, values);

            return new JsValue(jsArray);
        }

        /// <summary>
        /// Reduces a flat list of parameters to a params array
        /// </summary>
        private JsValue[] ProcessParamsArrays(JsValue[] jsArguments, MethodInfo[] methodInfos)
        {
            for (int i = 0, len = methodInfos.Length; i < len; i++)
            {
                var methodInfo = methodInfos[i];
                var parameters = methodInfo.GetParameters();
                if (!AnyHasAttribute<ParamArrayAttribute>(parameters))
                {
                    continue;
                }
                
                var nonParamsArgumentsCount = parameters.Length - 1;
                if (jsArguments.Length < nonParamsArgumentsCount)
                {
                    continue;
                }

                var argsToTransform = jsArguments.Slice(nonParamsArgumentsCount);
                if (argsToTransform.Length == 1 && argsToTransform[0].IsArray())
                {
                    continue;
                }

                var jsArray = NewJsArray(argsToTransform);
                var result = jsArguments.SliceAppend(jsArray, 0, nonParamsArgumentsCount);

                return result;
            }

            return jsArguments;
        }

    }
}
