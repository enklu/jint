using System;
using CreateAR.EnkluPlayer.DataStructures;

namespace Jint
{
    public class StrictModeScope : IOptimizedObjectPoolElement
    {
        private bool _strict;
        private bool _force;
        private int _forcedRefCount;

        [ThreadStatic] 
        private static int _refCount;
        
        public void Setup(bool strict = true, bool force = false)
        {
            _strict = strict;
            _force = force;

            if (_force)
            {
                _forcedRefCount = _refCount;
                _refCount = 0;
            }

            if (_strict)
            {
                _refCount++;
            }
        }

        public void Teardown()
        {
            if (_strict)
            {
                _refCount--;
            }

            if (_force)
            {
                _refCount = _forcedRefCount;
            } 
        }

        public static bool IsStrictModeCode
        {
            get { return _refCount > 0; }
        }

        public static int RefCount
        {
            get
            {
                return _refCount;
            }
            set
            {
                _refCount = value;
            }
        }

        public int Index { get; set; }
        public bool Available { get; set; }
    }
}
