//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.WindowsAzure.Management.Marketplace.Rest.WebAppGallery
{
    internal class PackageParameter
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsPassword { get; set; }

        public bool IsEnumeration { get; set; }

        public bool IsHidden { get; set; }

        public bool IsApplicationPath { get; set; }
        public bool IsAppUrl { get; set; }

        public bool IsDbParameter { get; set; }

        public bool AllowEmpty { get; set; }

        public string[] ValidationValues { get; set; }

        public string RegEx { get; set; }

        public string Value { get; set; }

        public string[] Tags { get; set; }

        public long WellKnownTags { get; set; }
    }
}
