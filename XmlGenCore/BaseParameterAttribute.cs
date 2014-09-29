using System;
using System.Collections.Generic;

namespace XmlGenCore
{
    public abstract class BaseParameterAttribute : Attribute
    {
        public abstract string ParameterName { get; }
        public abstract List<string> RequestType { get; }
        public abstract bool IsRequired { get; }
        public abstract bool AllowsMultiple { get; }
        public abstract string HelpText { get; }
    }
}