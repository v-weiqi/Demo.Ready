using System;
using System.Globalization;
using System.Xml;

namespace XmlGenCore
{
    public static class XmlUtil
    {
        public static void WriteIfNotNull(this XmlWriter writer, string localName, string elementString)
        {
            if (elementString != null)
            {
                writer.WriteElementString(localName, elementString);
            }
        }

        public static void WriteIfNotNull(this XmlWriter writer, string localName, int? value)
        {
            if (value != null && value.HasValue)
            {
                writer.WriteElementString(localName, value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static void WriteResourceElement(this XmlWriter writer, string localName, string value, string resourceNameAttribute, string resourceNameValue)
        {
            if (value == null && resourceNameValue == null)
            {
                return;
            }

            writer.WriteStartElement(localName);
            if (!string.IsNullOrEmpty(resourceNameValue))
            {
                writer.WriteAttributeString(resourceNameAttribute, resourceNameValue);
            }
            writer.WriteString(value);
            writer.WriteEndElement();
        }

        public static void WriteIfNotNull(this XmlWriter writer, string localName, bool? value)
        {
            if (value != null && value.HasValue)
            {
                writer.WriteElementString(localName, value.Value.ToString());
            }
        }

        internal static void WriteIfNotNull(this XmlWriter writer, string localName, DateTime? value)
        {
            if (value != null && value.HasValue)
            {
                writer.WriteElementString(localName, value.Value.ToString("s") + "Z");
            }
        }

        internal static string Get(this XmlElement element, string elementName)
        {
            XmlElement childElement = element[elementName, Helper.Namespace];
            if (childElement == null)
            {
                return null;
            }

            return childElement.InnerText;
        }

        internal static string GetElementString(this XmlElement element, string elementName)
        {
            XmlElement childElement = element[elementName];
            if (childElement == null)
            {
                return null;
            }

            return childElement.InnerText;
        }

        internal static DateTime GetDate(this XmlElement element, string elementName, DateTime defaultValue)
        {
            string updatedString = element.Get(elementName);
            if (!String.IsNullOrEmpty(updatedString))
            {
                return DateTime.Parse(updatedString);
            }

            return defaultValue;
        }

        internal static DateTime? GetDate(this XmlElement element, string elementName)
        {
            string updatedString = element.Get(elementName);
            if (!String.IsNullOrEmpty(updatedString))
            {
                return DateTime.Parse(updatedString);
            }

            return null;
        }

        internal static short? GetShort(this XmlElement element, string elementName)
        {
            string updatedString = element.Get(elementName);
            if (!String.IsNullOrEmpty(updatedString))
            {
                return short.Parse(updatedString);
            }

            return null;
        }

        internal static int? GetInt(this XmlElement element, string elementName)
        {
            string updatedString = element.Get(elementName);
            if (!String.IsNullOrEmpty(updatedString))
            {
                return int.Parse(updatedString);
            }

            return null;
        }

        internal static bool? GetBool(this XmlElement element, string elementName)
        {
            string updatedString = element.Get(elementName);
            if (!String.IsNullOrEmpty(updatedString))
            {
                return bool.Parse(updatedString);
            }

            return null;
        }

        internal static bool GetBoolAttribute(this XmlElement element, string attributeName, bool defaultValue)
        {
            string updatedString = element.GetAttribute(attributeName);
            if (!String.IsNullOrEmpty(updatedString))
            {
                return bool.Parse(updatedString);
            }

            return defaultValue;
        }

        internal static string GetElementAttribute(this XmlElement element, string elementName, string attributeName)
        {
            XmlElement childElement = element[elementName, Helper.Namespace];
            if (childElement == null)
            {
                return null;
            }

            XmlAttribute attribute = childElement.Attributes[attributeName];
            if (attribute == null)
            {
                return null;
            }

            return attribute.Value;
        }
    }
}
