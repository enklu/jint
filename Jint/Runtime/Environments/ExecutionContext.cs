﻿using Jint.Native;
using Jint.Runtime.Memory;

namespace Jint.Runtime.Environments
{
    public sealed class ExecutionContext : IOptimizedObjectPoolElement
    {
        public LexicalEnvironment LexicalEnvironment { get; set; }
        public LexicalEnvironment VariableEnvironment { get; set; }

        public JsValue ThisBinding { get; set; }

        public int Index { get; set; }
        public bool Available { get; set; }
    }
}
