using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jint.Runtime.Memory
{
    public static class OptimizationExtensions
    {
        /// <summary>
        /// This extension method adds a specific range from an array to a list. Range validation
        /// is provided by the array access.
        /// </summary>
        public static List<T> ListSlice<T>(this T[] arr, int start, int count = -1)
        {
            if (count < 0)
            {
                count = arr.Length - start;
            }

            var list = new List<T>();
            for (int i = start; i < (start + count); ++i)
            {
                list.Add(arr[i]);
            }

            return list;
        }

        /// <summary>
        /// Creates a new array using the range provided. 
        /// </summary>
        public static T[] Slice<T>(this T[] arr, int start, int count = -1)
        {
            if (count < 0)
            {
                count = arr.Length - start;
            }

            var newArr = new T[count];
            var index = 0;
            for (int i = start; i < (start + count); ++i)
            {
                newArr[index++] = arr[i];
            }

            return newArr;
        }

        /// <summary>
        /// Creates a new array using the range provided plus an additional appended element.
        /// </summary>
        public static T[] SliceAppend<T>(this T[] arr, T appendElement, int start, int count = -1)
        {
            if (count < 0)
            {
                count = arr.Length - start;
            }

            var newArr = new T[count + 1];
            var index = 0;
            for (int i = start; i < (start + count); ++i)
            {
                newArr[index++] = arr[i];
            }

            newArr[index] = appendElement;

            return newArr;
        }

        /// <summary>
        /// Adds a specific range of elements from the <see cref="elements"/> array to the list.
        /// </summary>
        public static void AddSubRange<T>(this List<T> list, T[] elements, int start, int end = -1)
        {
            end = end < 0 ? elements.Length : end;

            for (int i = start; i < end; ++i)
            {
                list.Add(elements[i]);
            }
        }
    }
}