using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Runtime.Memory;
using Jint.Runtime.References;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a Liexical Environment (a.k.a Scope)
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.2
    /// </summary>
    public sealed class LexicalEnvironment 
    {
        public EnvironmentRecord Record { get; private set; }

        public LexicalEnvironment Outer { get; private set; }

        public void Setup(EnvironmentRecord record, LexicalEnvironment outer)
        {
            Record = record;
            Outer = outer;
        }

        public static Reference GetIdentifierReference(LexicalEnvironment lex, string name, bool strict)
        {
            if (lex == null)
            {
                return new Reference(Undefined.Instance, name, strict);
            }

            if (lex.Record.HasBinding(name))
            {
                return new Reference(lex.Record, name, strict);
            }

            if (lex.Outer == null)
            {
                return new Reference(Undefined.Instance, name, strict);    
            }

            return GetIdentifierReference(lex.Outer, name, strict);
        }

        /// <summary>
        /// Creates a new Declarative LexicalEnvironment instance.
        /// </summary>
        public static LexicalEnvironment NewDeclarativeEnvironment(Engine engine, LexicalEnvironment outer = null)
        {
            var env = new LexicalEnvironment();
            env.Setup(new DeclarativeEnvironmentRecord(engine), outer);
            return env;
        }
    }

    
}
