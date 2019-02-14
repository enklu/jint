using System;
using System.Collections.Generic;

namespace Jint.Runtime.Memory
{
    /// <summary>
    /// Describes an element that can be in an <c>OptimizedObjectPool</c>.
    /// </summary>
    public interface IOptimizedObjectPoolElement
    {
        /// <summary>
        /// An index used by the <c>OptimizedObjectPool</c>.
        /// </summary>
        int Index { get; set; }

        /// <summary>
        /// Used to determine if this has already been put back into a pool.
        /// </summary>
        bool Available { get; set; }
    }

    /// <summary>
    /// Simple object wrapper for any element.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OptimizedObjectPoolWrapper<T> : IOptimizedObjectPoolElement
    {
        /// <inheritdoc />
        public int Index { get; set; }

        /// <inheritdoc />
        public bool Available { get; set; }

        /// <summary>
        /// The value we're wrapping/
        /// </summary>
        public T Value { get; private set; }
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="value">The internal value.</param>
        public OptimizedObjectPoolWrapper(T value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Very simple object pool that uses an interface for storing lookup index.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    public class OptimizedObjectPool<T> where T : IOptimizedObjectPoolElement
    {
        /// <summary>
        /// Maximum number to allocate.
        /// </summary>
        private readonly int _max;

        /// <summary>
        /// Number to grow by.
        /// </summary>
        private readonly int _growSize;

        /// <summary>
        /// Function to create instances.
        /// </summary>
        private readonly Func<T> _factory;

        /// <summary>
        /// All allocated instances.
        /// </summary>
        private readonly List<T> _allocated = new List<T>();

        /// <summary>
        /// Queue of available indices.
        /// </summary>
        private readonly Queue<int> _availableIndices = new Queue<int>();

        /// <summary>
        /// Number of elements currently allocated.
        /// </summary>
        public int Size
        {
            get { return _allocated.Count; }
        }

        /// <summary>
        /// Number of elements currently being used.
        /// </summary>
        public int Used
        {
            get { return Size - _availableIndices.Count; }
        }

        /// <inheritdoc />
        public OptimizedObjectPool(int min, Func<T> factory)
            : this(min, 0, 4, factory)
        { }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="min">Minimum number of instances to create.</param>
        /// <param name="max">Maximum number of instances to create or 0 if unlimited.</param>
        /// <param name="growSize">The number to grow by or 0 for a static pool.</param>
        /// <param name="factory">The function to create instances.</param>
        public OptimizedObjectPool(int min, int max, int growSize, Func<T> factory)
        {
            _max = max;
            _growSize = growSize;
            _factory = factory;

            for (var i = 0; i < min; i++)
            {
                var instance = _factory();
                instance.Index = _allocated.Count;
                instance.Available = true;

                _allocated.Add(instance);
                _availableIndices.Enqueue(instance.Index);
            }
        }

        /// <summary>
        /// Retrieves an instance or default(T) if none are available.
        /// </summary>
        /// <returns></returns>
        public T Get()
        {
            // grow
            var availableCount = _availableIndices.Count;
            if (0 == availableCount)
            {
                if (_max <= 0 || Size < _max)
                {
                    if (!Grow())
                    {
                        return default(T);
                    }
                }
                else
                {
                    return default(T);
                }
            }

            // pick available
            var index = _availableIndices.Dequeue();
            var instance = _allocated[index];
            instance.Available = false;

            return instance;
        }

        /// <summary>
        /// Puts an instance back into the pool.
        /// </summary>
        /// <param name="instance">The instance.</param>
        public void Put(T instance)
        {
            if (instance.Available)
            {
                return;
            }

            instance.Available = true;
            _availableIndices.Enqueue(instance.Index);
        }

        /// <summary>
        /// Destroys all instances. The pool will be emptied.
        /// </summary>
        /// <param name="destructor"></param>
        public void Destroy(Action<T> destructor)
        {
            for (int i = 0, len = _allocated.Count; i < len; i++)
            {
                destructor(_allocated[i]);
            }

            _allocated.Clear();
            _availableIndices.Clear();
        }

        /// <summary>
        /// Creates new instances.
        /// </summary>
        /// <returns></returns>
        private bool Grow()
        {
            var len = Math.Min(_growSize, _max > 0 ? _max - Size : _growSize);
            for (var i = 0; i < len; i++)
            {
                var instance = _factory();
                instance.Index = _allocated.Count;
                instance.Available = true;

                _allocated.Add(instance);
                _availableIndices.Enqueue(instance.Index);
            }

            return len > 0;
        }
    }
}