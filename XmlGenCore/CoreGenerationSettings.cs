using System;
using System.Collections.Generic;
using System.Reflection;

namespace XmlGenCore
{
    public class CoreGenerationSettings
    {
        public delegate string[] GetSettingsValues(string settingName);

        private string _feedLocation;
        private bool _customFeedSet;
        private bool _makeTestAppsKatalReady = true;
        private bool _makeTestAppsAzureReady = true;
        private bool _includeTestingApps = true;
        private List<string> _appIds;
        private List<string> _testAppIds;
        private FeedType _feedGenerationType = FeedType.Live;
        private FeedActivity _feedGenerationActivity = FeedActivity.GenerateWithAllApps;
        private readonly string _requestType;
        private List<string> _casePreservedAppIds;

        public CoreGenerationSettings()
            : this("waf", null)
        {
        }

        public CoreGenerationSettings(string type)
            : this(type, null)
        {
        }

        public CoreGenerationSettings(string type, GetSettingsValues method)
        {
            _requestType = type;
            Init(method);
        }

        [OptionalParameter("FeedType", "Live", new[] { "waf" }, false, "Supported feed types are: ", typeof(FeedType))]
        private string ReflectionSet_FeedGenerationType
        {
            set
            {
                FeedType selectedType;
                if (Enum.TryParse(value, true, out selectedType))
                {
                    _feedGenerationType = selectedType;
                }
            }
        }

        public FeedType FeedGenerationType
        {
            get
            {
                return _feedGenerationType;
            }
            set
            {
                _feedGenerationType = value;
            }
        }

        [OptionalParameter("FeedActivity", "Generate", new[] { "waf" }, false, "Supported feed activities are: ", typeof(FeedActivity))]
        private string ReflectionSet_FeedGenerationActivity
        {
            set
            {
                FeedActivity selectedType;
                if (Enum.TryParse(value, true, out selectedType))
                {
                    _feedGenerationActivity = selectedType;
                }
            }
        }

        public FeedActivity FeedGenerationActivity
        {
            get
            {
                return _feedGenerationActivity;
            }
        }

        [OptionalParameter("makeTestAppsKatalReady", "False", new[] { "waf" })]
        private string ReflectionSet_SetMakeTestAppsKatalReady
        {
            set
            {
                try
                {
                    _makeTestAppsKatalReady = Convert.ToBoolean(value);
                }
                catch { }
            }
        }

        public bool MarkTestAppsKatalReady
        {
            get
            {
                return _makeTestAppsKatalReady;
            }
            set
            {
                _makeTestAppsKatalReady = value;
            }
        }

        [OptionalParameter("makeTestAppsAzureReady", "False", new[] { "waf" })]
        private string ReflectionSet_SetMakeTestAppsAzureReady
        {
            set
            {
                try
                {
                    _makeTestAppsAzureReady = Convert.ToBoolean(value);
                }
                catch { }
            }
        }

        public bool MarkTestAppsAzureReady
        {
            get
            {
                return _makeTestAppsAzureReady;
            }
            set
            {
                _makeTestAppsAzureReady = value;
            }
        }

        public bool IncludeTestingApps
        {
            get
            {
                return _includeTestingApps;
            }
            set
            {
                _includeTestingApps = value;
            }
        }

        [RequiredParameter("appId", new[] { "rf" })]
        private string ReflectionSet_ResourceFeedAppId
        {
            set { ReflectionSet_AppIds = value; }
        }

        [OptionalParameter("appId", null, new[] { "waf" }, true)]
        private string ReflectionSet_AppIds
        {
            set
            {
                if (value != null)
                {
                    if (_appIds == null)
                    {
                        _appIds = new List<string>();
                        _casePreservedAppIds = new List<string>();
                    }

                    var temporaryAppIds = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var id in temporaryAppIds)
                    {
                        _appIds.Add(id.ToLower());
                        _casePreservedAppIds.Add(id);
                    }
                }
                else
                {
                    _appIds = new List<string>();
                }
            }
        }

        public List<string> AppIds
        {
            get
            {
                return _appIds;
            }
            set
            {
                if (value != null)
                {
                    _appIds = new List<string>();
                    _casePreservedAppIds = new List<string>();
                    foreach (var id in value)
                    {
                        _appIds.Add(id.ToLower());
                        _casePreservedAppIds.Add(id);
                    }
                }
            }
        }

        public List<string> AppIdsCasePreserved
        {
            get { return _casePreservedAppIds; }
        }

        [OptionalParameter("testAppId", null, new[] { "waf" }, true)]
        private string ReflectionSet_TestAppIdFilter
        {
            set
            {
                if (value != null)
                {
                    if (_testAppIds == null)
                    {
                        _testAppIds = new List<string>();
                    }

                    string[] temporaryAppIds = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var id in temporaryAppIds)
                    {
                        _testAppIds.Add(id.ToLower());
                    }
                }
                else
                {
                    _testAppIds = new List<string>();
                }
            }
        }

        public List<string> TestAppIdFilter
        {
            get
            {
                return _testAppIds;
            }
            set
            {
                if (value != null)
                {
                    _testAppIds = new List<string>();
                    foreach (string id in value)
                    {
                        _testAppIds.Add(id.ToLower());
                    }
                }
            }
        }

        [OptionalParameter("productFeed", null, new[] { "wpf" })]
        public string ProductFeedUrl
        {
            set
            {
                _feedLocation = value;
                _customFeedSet = true;
            }
            get
            {
                if (!_customFeedSet)
                {
                    _feedLocation = FeedInterface.GetFeed(_feedGenerationType);
                }
                return _feedLocation;
            }
        }

        private void Init(GetSettingsValues method)
        {
            var properties = typeof(CoreGenerationSettings).GetProperties(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var attribute = Attribute.GetCustomAttribute(property, typeof(BaseParameterAttribute)) as BaseParameterAttribute;
                if (attribute != null && attribute.RequestType.Contains(_requestType))
                {
                    var items = (method != null) ? method(attribute.ParameterName) : null;
                    var itemCount = (items == null) ? 0 : items.Length;
                    string value = null;
                    if ((itemCount > 1 && attribute.AllowsMultiple) || itemCount == 1)
                    {
                        foreach (var item in items)
                        {
                            value += item + ';';
                        }

                        value = value.TrimEnd(';');
                    }
                    else if (itemCount != 0)
                    {
                        throw new InvalidOperationException(String.Format("Only one parameter for '{0}' is allowed.", attribute.ParameterName));
                    }

                    if (value == null)
                    {
                        if (attribute.IsRequired)
                        {
                            throw new InvalidOperationException(String.Format("The query string parameter '{0}' was missing and is required.", attribute.ParameterName));
                        }
                        
                        var oAttrib = attribute as OptionalParameterAttribute;
                        if (oAttrib != null)
                        {
                            value = oAttrib.DefaultValue;
                        }
                    }

                    property.SetValue(this, value, null);
                }
            }
        }

        public bool TargetFeedIsAzure
        {
            get
            {
                return (_feedGenerationType == FeedType.PROD || _feedGenerationType == FeedType.TC2);
            }
        }
    }
}
