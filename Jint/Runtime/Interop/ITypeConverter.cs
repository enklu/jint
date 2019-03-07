using System;

namespace Jint.Runtime.Interop
{
    public interface ITypeConverter
    {
        /// <summary>
        /// Registers a delegate conversion operation for a specific target type. When the callable must be adapted to this type,
        /// use the provided conversion operation.
        /// </summary>
        /// <param name="targetType">The <see cref="Type"/> of the object Jint is trying to convert the callable to.</param>
        /// <param name="conversion">The conversion implementation to use for the specific target type</param>
        void RegisterDelegateConversion(Type targetType, ICallableConversion conversion);

        object Convert(object value, Type type, IFormatProvider formatProvider);
        bool TryConvert(object value, Type type, IFormatProvider formatProvider, out object converted);
    }
}
