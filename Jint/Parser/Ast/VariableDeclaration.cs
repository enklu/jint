using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class VariableDeclaration : Statement
    {
        public IList<VariableDeclarator> Declarations;
        public string Kind;
    }
}