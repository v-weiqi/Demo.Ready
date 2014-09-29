using System;
using System.Collections.Generic;
using System.Text;

namespace XmlGenCore
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RequiredParameterAttribute : BaseParameterAttribute
    {
        private readonly string _name;
        private readonly string[] _requestType;
        private readonly bool _allowsMultiple;
        private readonly string _helpText;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName">Name of query String parameter</param>
        /// <param name="requestType">Handle parameter is valid for</param>
        /// <param name="allowsMultiple">Can multiple query string parameters be submitted</param>
        public RequiredParameterAttribute(string parameterName, string[] requestType, bool allowsMultiple = false)
        {
            _name = parameterName;
            _requestType = requestType;
            _allowsMultiple = allowsMultiple;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName">Name of query String parameter</param>
        /// <param name="requestType">Handle parameter is valid for</param>
        /// <param name="allowsMultiple">Can multiple query string parameters be submitted</param>
        /// <param name="helpMessage">Help message to display</param>
        public RequiredParameterAttribute(
            string parameterName, string[] requestType, bool allowsMultiple, string helpMessage) :
            this(parameterName, requestType, allowsMultiple)
        {
            _helpText = helpMessage;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName">Name of query String parameter</param>
        /// <param name="requestType">Handle parameter is valid for</param>
        /// <param name="allowsMultiple">Can multiple query string parameters be submitted</param>
        /// <param name="helpMessage">Help message to display</param>
        /// <param name="enumType">Enum name will be concatenated with spaces.</param>
        public RequiredParameterAttribute(
            string parameterName, string[] requestType, bool allowsMultiple, string helpMessage, Type enumType) :
            this(parameterName, requestType, allowsMultiple)
        {
            var helpBuilder = new StringBuilder(helpMessage);
            foreach (var helpPiece in Enum.GetNames(enumType))
            {
                helpBuilder.AppendFormat(" '{0}',", helpPiece);
            }
            _helpText = helpBuilder.ToString().Trim(',');
        }

        public override string ParameterName
        {
            get { return _name; }
        }

        public override List<string> RequestType
        {
            get { return new List<string>(_requestType); }
        }

        public override bool IsRequired
        {
            get { return true; }
        }

        public override bool AllowsMultiple
        {
            get { return _allowsMultiple; }
        }

        public override string HelpText
        {
            get { return _helpText; }
        }
    }
}