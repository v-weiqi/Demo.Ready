using System;

namespace XmlGenCore
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple=false)]
    public class OptionalParameterAttribute : RequiredParameterAttribute
    {
        private readonly string _defaultValue;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName">Name of query String parameter</param>
        /// <param name="defaultValue">Value to assign to parameter if query string value is not set</param>
        /// <param name="requestType">Handle parameter is valid for</param>
        /// <param name="allowsMultiple">Can multiple query string parameters be submitted</param>
        public OptionalParameterAttribute(string parameterName, string defaultValue, string[] requestType, bool allowsMultiple = false) :
            base (parameterName, requestType, allowsMultiple)
        {
            _defaultValue = defaultValue;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName">Name of query String parameter</param>
        /// <param name="defaultValue">Value to assign to parameter if query string value is not set</param>
        /// <param name="requestType">Handle parameter is valid for</param>
        /// <param name="allowsMultiple">Can multiple query string parameters be submitted</param>
        /// <param name="helpMessage">Help message to display</param>
        public OptionalParameterAttribute(
            string parameterName, string defaultValue, string[] requestType, bool allowsMultiple, string helpMessage) :
            base(parameterName, requestType, allowsMultiple, helpMessage)
        {
            _defaultValue = defaultValue;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName">Name of query String parameter</param>
        /// <param name="defaultValue">Value to assign to parameter if query string value is not set</param>
        /// <param name="requestType">Handle parameter is valid for</param>
        /// <param name="allowsMultiple">Can multiple query string parameters be submitted</param>
        /// <param name="helpMessage">Help message to display</param>
        /// <param name="enumType">Enum name will be concatenated with spaces.</param>
        public OptionalParameterAttribute(
            string parameterName, string defaultValue, string[] requestType, bool allowsMultiple, string helpMessage, Type enumType) :
            base(parameterName, requestType, allowsMultiple, helpMessage, enumType)
        {
            _defaultValue = defaultValue;
        }
        
        public override bool IsRequired
        {
            get { return false; }
        }

        public string DefaultValue
        {
            get { return _defaultValue; }
        }
    }
}