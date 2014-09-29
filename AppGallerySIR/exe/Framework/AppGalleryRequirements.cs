using System;
using System.Collections.Generic;
using System.Text;

namespace AppGallery.SIR
{
    public class AppGalleryRequirements
    {
        #region Data members

        protected static List<string> _requiredProviders;
        protected static List<string> _optionalProviders;
        protected static List<string> _validCustomTags;

        #endregion

        #region Properties

        public static List<string> RequiredProviders
        {
            get
            {
                if (_requiredProviders == null)
                {
                    _requiredProviders = new List<string>();
                    _requiredProviders.Add("iisapp");
                    _requiredProviders.Add("setacl");
                }
                return _requiredProviders;
            }
        }

        public static List<string> OptionalProviders
        {
            get
            {
                if (_optionalProviders == null)
                {
                    _optionalProviders = new List<string>();
                    _optionalProviders.Add("dbmySql");
                    _optionalProviders.Add("dbfullsql");
                }
                return _optionalProviders;
            }
        }

        public static List<string> CustomTags
        {
            get
            {
                if (_validCustomTags == null)
                {
                    _validCustomTags = new List<string>();
                }
                return _validCustomTags;
            }
        }


        #endregion
    }
}
