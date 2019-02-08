using System;

namespace CreateAR.EnkluPlayer.Scripting
{
    /// <summary>
    /// Restricts method from being called by Js.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class DenyJsAccess : Attribute
    {
        //
    }
}