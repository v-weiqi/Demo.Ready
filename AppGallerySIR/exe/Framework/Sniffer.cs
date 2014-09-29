using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;

using Ionic.Zip;
using Microsoft.Web.Deployment;

using LogEventType = AppGallery.SIR.ILog.LogEvent.LogEventType;

// GZipStream Class
// http://msdn.microsoft.com/en-us/library/system.io.compression.gzipstream.aspx
//
// Shell solution
// http://social.msdn.microsoft.com/Forums/en-US/csharpgeneral/thread/b5bde5fa-f63a-41e0-8569-c75d2f61e76b
//
// DotNetZip
// http://dotnetzip.codeplex.com/releases/view/27890
// http://blogs.msdn.com/dotnetinterop/archive/2006/04/05/.NET-System.IO.Compression-and-zip-files.aspx

namespace AppGallery.SIR
{
    public class Sniffer
    {
        protected List<DeploymentWellKnownTag> RequiredDbTags = new List<DeploymentWellKnownTag>(new DeploymentWellKnownTag[] 
        {
            DeploymentWellKnownTag.DBAdminUserName,
            DeploymentWellKnownTag.DBAdminPassword,
            DeploymentWellKnownTag.DBServer,
            DeploymentWellKnownTag.DBName,
            DeploymentWellKnownTag.DBUserName,
            DeploymentWellKnownTag.DBUserPassword
        });

        #region Data Members & Constructors

        protected List<string> _parameterEntryMessages;
        protected List<string> _manifestEntryMessages;
        protected List<DeploymentWellKnownTag> _cachedTags;
        protected PackageValidationManager _packageValidationManager;
        private bool _noSchemaParameterErrors = true;

        public Sniffer(PackageValidationManager packageValidationManager)
        {
            _packageValidationManager = packageValidationManager;
        }

        #endregion

        public List<DeploymentWellKnownTag> CachedTags
        {
            get
            {
                if (_cachedTags == null)
                {
                    _cachedTags = new List<DeploymentWellKnownTag>();
                }
                return _cachedTags;
            }
        }

        public bool ReadZipPackage(string packagePhysicalPath)
        {
            try
            {
                Package.Current.CreateZipPackage();
                _packageValidationManager.Reporter.Log(LogEventType.Pass, "Successfully unzipped the package");
                return true;
            }
            catch (Exception e)
            {
                _packageValidationManager.Reporter.Log(LogEventType.Fail, "Failed to read the file <" + Package.Current.PackagePath + ">");
                _packageValidationManager.Reporter.Log(e);
                return false;
            }
        }
        
        public bool ValidateManifestFileExists()
        {
            if (Package.Current.DoesManifestFileExist())
            {
                _packageValidationManager.Reporter.Log(LogEventType.Pass, "The manifest.xml file is present.");
                return true;
            }
            else
            {
                _packageValidationManager.Reporter.Log(LogEventType.Fail, "The manifest.xml file is not present.");
                return false;
            }
        }

        public bool ValidateParameterTags()
        {
            bool pass = true;
            XmlTextReader xmlTextReader = null;
            try
            {
                _cachedTags = new List<DeploymentWellKnownTag>();
                using (StreamReader streamReader = new StreamReader(Package.Current.ParametersMemoryStream))
                {
                    xmlTextReader = new XmlTextReader(streamReader);
                    while (xmlTextReader.Read())
                    {
                        string tagsAttributeValue;
                        if (xmlTextReader.Name.Equals("parameter", StringComparison.OrdinalIgnoreCase) &&
                            xmlTextReader.NodeType == XmlNodeType.Element)
                        {
                            tagsAttributeValue = xmlTextReader.GetAttribute("tags");
                            if (tagsAttributeValue != null)
                            {
                                tagsAttributeValue = tagsAttributeValue.Trim();
                            }
                            if (!string.IsNullOrEmpty(tagsAttributeValue))
                            {
                                List<string> validTags;
                                List<string> invalidTags;
                                if (IsValidSetOfTags(tagsAttributeValue, out validTags, out invalidTags))
                                {
                                    _packageValidationManager.Reporter.Log(LogEventType.Pass, "Tag \"" + tagsAttributeValue + "\" is valid.", xmlTextReader.LineNumber.ToString(), Package.Current.ParametersFile);
                                }
                                else
                                {
                                    _packageValidationManager.Reporter.Log(LogEventType.Informational,
                                                "Tag \"" + tagsAttributeValue + "\" is unrecognized. " +
                                                (validTags.Count > 0 ? "Recognized tags: " + string.Join(",", validTags.ToArray()) + ". " : "") +
                                                (validTags.Count > 0 && invalidTags.Count > 0 ? "Unrecognized tags: " + string.Join(",", invalidTags.ToArray()) + "." : ""),
                                                xmlTextReader.LineNumber.ToString(),
                                                Package.Current.ParametersFile);

                                    pass = true; // do not fail on unrecognized tags
                                }
                            }
                            else
                            {
                                _packageValidationManager.Reporter.Log(LogEventType.Informational, "The parameter " + xmlTextReader.Value + " does not have tags", xmlTextReader.LineNumber.ToString(), Package.Current.ParametersFile);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _packageValidationManager.Reporter.Log(e);
                pass = false;
            }
            finally
            {
                if (xmlTextReader != null)
                {
                    xmlTextReader.Close();
                }
            }
            return pass;
        }

        private bool IsValidSetOfTags(string tagsAttributeValue, out List<string> validTags, out List<string> invalidTags)
        {
            // Spilt tags "SQL, Hidden" and verify it is part of expected valid tags 
            bool pass = true;
            validTags = new List<string>();
            invalidTags = new List<string>();
            try
            {
                string[] actualTags = tagsAttributeValue.Split(new char[] { ',' });
                foreach (string tag in actualTags)
                {
                    string sanitizedTag = tag.Trim();
                    try
                    {
                        DeploymentWellKnownTag knownTag = (DeploymentWellKnownTag)Enum.Parse(typeof(DeploymentWellKnownTag), sanitizedTag, true);
                        validTags.Add(sanitizedTag);
                        if (!_cachedTags.Contains(knownTag))
                        {
                            _cachedTags.Add(knownTag);
                        }
                    }
                    catch (Exception)
                    {
                        if (IsTagOneOfCustomTags(sanitizedTag))
                        {
                            validTags.Add(sanitizedTag);
                            _packageValidationManager.Reporter.Log(LogEventType.Informational, "Found custom tag: " + tag);
                        }
                        else
                        {
                            invalidTags.Add(sanitizedTag);
                            pass = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _packageValidationManager.Reporter.Log(e);
                pass = false;
            }
            return pass;
        }

        public bool IsTagOneOfCustomTags(string tag)
        {
            foreach (string customTag in AppGalleryRequirements.CustomTags)
            {
                if (customTag.Equals(tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ValidateParametersFile()
        {
            bool pass = true;
            XmlReader xmlReader = null;
            StreamReader streamReader = new StreamReader(Package.Current.ParametersMemoryStream);
            XmlReader schemaDocument = XmlReader.Create(PackageValidationManager.ParametersXSDTextStreamReader);

            if (!ValidateParametersFileExists())
            {
                return false;
            }                        

            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();   
                settings.Schemas.Add(null, schemaDocument);
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationEventHandler += new ValidationEventHandler(reader_ValidationEventHandler);
                xmlReader = XmlReader.Create(streamReader, settings);

                // read the whole file
                while (xmlReader.Read()) { ; }
            }
            catch (Exception e)
            {
                _packageValidationManager.Reporter.Log(e);
                pass = false;
            }
            finally
            {                
                if (xmlReader != null)
                {
                    xmlReader.Close();
                }

                // if not already failure due to exception, check whether there are schema validation errors
                if (pass)
                {
                    pass = _noSchemaParameterErrors;
                }

                // reset the flag
                _noSchemaParameterErrors = true;
            }
            return pass;
        }

        private void reader_ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            _noSchemaParameterErrors = false;
            _packageValidationManager.Reporter.Log
                (
                    LogEventType.Fail, 
                    e.Exception.Message, 
                    "(" + e.Exception.LineNumber.ToString() + ", " + e.Exception.LinePosition + ")",
                    "parameters.xml"
                );
        }

        public bool ValidateParametersFileExists()
        {
            if (Package.Current.DoesParametersFileExist())
            {
                _packageValidationManager.Reporter.Log(LogEventType.Pass, "The parameters.xml file is present");
                return true;
            }
            else
            {
                _packageValidationManager.Reporter.Log(LogEventType.Informational, "The parameters.xml file is not present");
                return false;
            }
        }

        public bool ValidateDeploymentObject()
        {
            try
            {
                if (!Helper.IsMsDeployInstalled())
                {
                    _packageValidationManager.Reporter.Log(LogEventType.Fail,
                        "MSDeploy is not installed, cannot proceed creation of DeploymentObject");
                    return false;
                }

                Package.Current.CreateDeploymentObject();
                _packageValidationManager.Reporter.Log(LogEventType.Pass, "Successfully created the Web Deployment Tool DeploymentObject.");
                return true;
            }
            catch (Exception e)
            {
                _packageValidationManager.Reporter.Log(LogEventType.Fail, "Unable to create the Web Deploymnet Tool DeploymentObject.");
                _packageValidationManager.Reporter.Log(e);
                return false;
            }
        }

        // Should be called after ValidateParametersTags method, so that parameter tags are cached
        public bool ValidateProviders()
        {
            bool pass = true;
            try
            {
                List<string> foundRequiredProviders = new List<string>();
                List<string> foundOptionalProviders = new List<string>();

                // 1. Find all required providers
                foreach (string requiredProvider in AppGalleryRequirements.RequiredProviders)
                {
                    if (!Package.Current.ContainsProvider(requiredProvider))
                    {
                        _packageValidationManager.Reporter.Log(LogEventType.Fail, "Missing required provider: " + requiredProvider);
                        pass = false;
                    }
                    else
                    {
                        foundRequiredProviders.Add(requiredProvider);
                    }
                }

                // 2. Find all optionals providers
                foreach (string optionalProvider in AppGalleryRequirements.OptionalProviders)
                {
                    if (Package.Current.ContainsProvider(optionalProvider))
                    {
                        foundOptionalProviders.Add(optionalProvider);
                    }
                }

                // 3. Find all providers that are not specified
                List<string> allFoundProviders = new List<string>(foundRequiredProviders);
                allFoundProviders.AddRange(foundOptionalProviders);
                List<DeploymentObject> nonSpecifiedProviders = Package.Current.GetNonSpecifiedProviders(allFoundProviders);
                foreach (DeploymentObject nonSpecifiedProvider in nonSpecifiedProviders)
                {
                    _packageValidationManager.Reporter.Log(LogEventType.Fail, "The following provider is not allowed: " + nonSpecifiedProvider.Name);
                    pass = false;
                }

                // 4. Validate DB tags
                bool dbParametersValidated = false;
                foreach (string foundOptionalProvider in foundOptionalProviders)
                {
                    if (IsDbProvider(foundOptionalProvider))
                    {
                        _packageValidationManager.Reporter.Log("The package contains a " + foundOptionalProvider + " database provider");
                        if (!dbParametersValidated)
                        {
                            // in case if more than 1 db provider, do not validate parameters twice
                            if (!ValidateDatabaseTags())
                            {
                                pass = false;
                            }
                            dbParametersValidated = true;
                        }
                    }
                    else
                    {
                        _packageValidationManager.Reporter.Log("The following optional provider(s) were found in the manifest.xml file: " + String.Join(", ", (string[])foundOptionalProviders.ToArray()));
                    }
                }
            }
            catch (Exception e)
            {
                _packageValidationManager.Reporter.Log(e);
                pass = false;
            }
            if (pass)
            {
                _packageValidationManager.Reporter.Log(LogEventType.Pass, "All required providers were present");
            }
            return pass;
        }

        public bool IsDbProvider(string provider)
        {
            return (provider.Equals(DeploymentWellKnownProvider.DBFullSql.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    provider.Equals(DeploymentWellKnownProvider.DBMySql.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        private bool ValidateDatabaseTags()
        {
            bool pass = true;
            foreach (DeploymentWellKnownTag requiredDbTag in RequiredDbTags)
            {
                if (!CachedTags.Contains(requiredDbTag))
                {
                    _packageValidationManager.Reporter.Log(LogEventType.Fail, "The package does not contain a required database tagged parameter: " + requiredDbTag);
                    pass = false;
                }
            }
            if (pass)
            {
                _packageValidationManager.Reporter.Log(LogEventType.Pass, "The package contains all " + RequiredDbTags.Count + " required database parameters properly tagged.");
            }

            return pass;
        }

        private bool HasConnectionStringTags(string parameterTags)
        {
            string[] tags = parameterTags.Split(',');

            foreach (string tag in tags)
            {                
                if (tag.Trim().Equals(DeploymentWellKnownTag.DBAdminPassword.ToString(), StringComparison.OrdinalIgnoreCase)
                    || tag.Trim().Equals(DeploymentWellKnownTag.DBAdminUserName.ToString(), StringComparison.OrdinalIgnoreCase)
                    || tag.Trim().Equals(DeploymentWellKnownTag.DBName.ToString(), StringComparison.OrdinalIgnoreCase)
                    || tag.Trim().Equals(DeploymentWellKnownTag.DBServer.ToString(), StringComparison.OrdinalIgnoreCase)
                    || tag.Trim().Equals(DeploymentWellKnownTag.DBUserName.ToString(), StringComparison.OrdinalIgnoreCase)
                    || tag.Trim().Equals(DeploymentWellKnownTag.DBUserPassword.ToString(), StringComparison.OrdinalIgnoreCase)
                    || tag.Trim().Equals(DeploymentWellKnownTag.SqlConnectionString.ToString(), StringComparison.OrdinalIgnoreCase)
                    || tag.Trim().Equals(DeploymentWellKnownTag.MySqlConnectionString.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
            }

            return false;
        }

        private bool HasSQLTag(string parameterTags)
        {
            string[] tags = parameterTags.Split(',');

            foreach (string tag in tags)
            {
                if (tag.Trim().Equals(DeploymentWellKnownTag.Sql.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasMYSQLTag(string parameterTags)
        {
            string[] tags = parameterTags.Split(',');

            foreach (string tag in tags)
            {
                if (tag.Trim().Equals(DeploymentWellKnownTag.MySql.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void DumpAcls()
        {
            IEnumerable<DeploymentObject> setAcls = Package.Current.GetProvidersByName("setacl");
            foreach (DeploymentObject setAcl in setAcls)
            {
                _packageValidationManager.Reporter.Log(LogEventType.Informational, "The package sets an ACL on " + setAcl.AbsolutePath +
                    // if setAclAccess is specified, dump its value, otherwise the default is Read
                    (setAcl.ProviderContext.ProviderSettings["setAclAccess"] != null ? " with " + setAcl.ProviderContext.ProviderSettings["setAclAccess"].Value.ToString() : "Read") + " permissions for " +
                    // if the setAclUser is specified, dump its value, otherwise the default is application pool identity
                    ((setAcl.ProviderContext.ProviderSettings["setAclUser"] != null && setAcl.ProviderContext.ProviderSettings["setAclUser"].Value != null) ? setAcl.ProviderContext.ProviderSettings["setAclUser"].Value.ToString() : "application pool identity"));
            }
        }      

        public bool ValidateManifestPathExistsInPackage()
        {
            bool pass = true;

            try
            {
                foreach (DeploymentObject child in Package.Current.MSDeployDeploymentObject.GetChildren())
                {
                    if (Package.Current.DoesFileExist(child.AbsolutePath))
                    {
                        _packageValidationManager.Reporter.Log(LogEventType.Pass, "Path " 
                            + child.AbsolutePath + " exists in package");
                    }
                    else
                    {
                        _packageValidationManager.Reporter.Log(LogEventType.Fail, "Path " 
                            + child.AbsolutePath + " does not exist in package");
                        pass = false;
                    }
                }
            }
            catch(Exception e)
            {
                pass = false;
                _packageValidationManager.Reporter.Log(e);
            }

            return pass;
        }

        public bool ValidateManifestEntries()
        {
            bool pass = true;
            bool found;
            
            try
            {
                DeploymentSyncParameterCollection parameterCollection
                    = Package.Current.MSDeployDeploymentObject.SyncParameters;

                foreach (DeploymentObject child in Package.Current.MSDeployDeploymentObject.GetChildren())
                {
                    _manifestEntryMessages = new List<string>();
                    _manifestEntryMessages.Add("Validating manifest entry : <" + child.Name + " path=\"" + child.AbsolutePath + "\"");
                    found = false;

                    foreach (DeploymentSyncParameter parameter in parameterCollection)
                    {
                        IEnumerable<DeploymentSyncParameterEntry> parameterEntries = parameter.Entries;

                        foreach (DeploymentSyncParameterEntry parameterEntry in parameterEntries)
                        {
                            if (parameterEntry.Kind == DeploymentSyncParameterEntryKind.ProviderPath)
                            {
                                if (Regex.IsMatch(child.Name, parameterEntry.Scope, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
                                    && Regex.IsMatch(child.AbsolutePath, parameterEntry.Match, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
                                {
                                    found = true;
                                    _manifestEntryMessages.Add(
                                        "Found corresponding parameterEntry : " 
                                        + "<parameterEntry type=\"" + parameterEntry.Kind + "\" scope=\"" + parameterEntry.Scope 
                                        + "\" match=\"" + parameterEntry.Match + "\" />");
                                }
                            }
                        }
                    }

                    if (found == false)
                    {
                        pass = false;
                        _manifestEntryMessages.Add("Unable to find corresponding parameterEntry for entry in manifest");
                        _packageValidationManager.Reporter.Log(LogEventType.Fail, _manifestEntryMessages);
                    }
                    else
                    {
                        _packageValidationManager.Reporter.Log(LogEventType.Pass, _manifestEntryMessages);
                    }
                }
            }
            catch (Exception e)
            {
                pass = false;
                _packageValidationManager.Reporter.Log(e);
            }

            return pass;
        }

        private bool DependantParameterOfConnectionStringParameter(string parameterDefaultValue)
        {
            if (parameterDefaultValue != null)
            {
                DeploymentSyncParameterCollection parameterCollection
                        = Package.Current.MSDeployDeploymentObject.SyncParameters;

                foreach (DeploymentSyncParameter parameter in parameterCollection)
                {
                    if (parameterDefaultValue.Contains('{' + parameter.Name + '}')
                        && HasConnectionStringTags(parameter.Tags))
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        public bool ValidateParameterEntries()
        {
            bool pass = true;
            bool isProviderPath;
            int dbAdminUserSQLParameters = 0;
            int dbAdminUserMYSQLParameters = 0;
            int dbAdminPasswordSQLParameters = 0;
            int dbAdminPasswordMYSQLParameters = 0;

            try
            {
                DeploymentSyncParameterCollection parameterCollection
                    = Package.Current.MSDeployDeploymentObject.SyncParameters;

                foreach (DeploymentSyncParameter parameter in parameterCollection)
                {
                    if (parameter.Tags.ToLower().Contains(DeploymentWellKnownTag.DBAdminUserName.ToString().ToLower()))
                    {
                        if (HasSQLTag(parameter.Tags))
                        {
                            dbAdminUserSQLParameters++;
                        }

                        if (HasMYSQLTag(parameter.Tags))
                        {
                            dbAdminUserMYSQLParameters++;
                        }
                    }

                    if (parameter.Tags.ToLower().Contains(DeploymentWellKnownTag.DBAdminPassword.ToString().ToLower()))
                    {
                        if (HasSQLTag(parameter.Tags))
                        {
                            dbAdminPasswordSQLParameters++;
                        }

                        if (HasMYSQLTag(parameter.Tags))
                        {
                            dbAdminPasswordMYSQLParameters++;
                        }
                    }

                    IEnumerable<DeploymentSyncParameterEntry> parameterEntries = parameter.Entries;

                    isProviderPath = false;

                    foreach (DeploymentSyncParameterEntry parameterEntry in parameterEntries)
                    {
                        if (parameterEntry.Kind == DeploymentSyncParameterEntryKind.ProviderPath)
                        {
                            isProviderPath = true;
                        }

                        _parameterEntryMessages = new List<string>();

                        _parameterEntryMessages.Add("Parameter: " + parameter.Name);

                        _parameterEntryMessages.Add(
                            "Validating parameterEntry: <parameterEntry type=\"" + parameterEntry.Kind + "\" scope=\"" + parameterEntry.Scope
                            + "\" match=\"" + parameterEntry.Match + "\" />");

                        if (!ValidateParameterEntry(parameterEntry))
                        {
                            _parameterEntryMessages.Add("Parameter Entry validation failed");
                            _packageValidationManager.Reporter.Log(LogEventType.Fail, _parameterEntryMessages);
                            pass = false;
                        }
                        else
                        {
                            _parameterEntryMessages.Add("Parameter Entry validation passed");
                            _packageValidationManager.Reporter.Log(LogEventType.Pass, _parameterEntryMessages);
                        }
                    }

                    if (!isProviderPath && !HasConnectionStringTags(parameter.Tags)
                        && !DependantParameterOfConnectionStringParameter(parameter.DefaultValue))
                    {
                        if (parameter.Tags.ToLower().Contains(DeploymentWellKnownTag.NoStore.ToString().ToLower()))
                        {
                            _packageValidationManager.Reporter.Log(LogEventType.Pass,
                                "Parameter " + parameter.Name + " already tagged NoStore");
                        }
                        else
                        {
                            _packageValidationManager.Reporter.Log(LogEventType.Fail,
                                "Parameter " + parameter.Name + " should be tagged NoStore");
                            pass = false;
                        }        
                    }
                }

                if (dbAdminPasswordSQLParameters > 1)
                {
                    _packageValidationManager.Reporter.Log(LogEventType.Fail,
                        "More than one SQL parameter tagged DbAdminPassword");
                }

                if (dbAdminUserSQLParameters > 1)
                {
                    _packageValidationManager.Reporter.Log(LogEventType.Fail,
                        "More than one SQL parameter is tagged DbAdminUser");
                }

                if (dbAdminPasswordMYSQLParameters > 1)
                {
                    _packageValidationManager.Reporter.Log(LogEventType.Fail,
                        "More than one MYSQL parameter tagged DbAdminPassword");
                }

                if (dbAdminUserMYSQLParameters > 1)
                {
                    _packageValidationManager.Reporter.Log(LogEventType.Fail,
                        "More than one MYSQL parameter tagged DbAdminUser");
                }
            }
            catch (Exception e)
            {
                pass = false;
                _packageValidationManager.Reporter.Log(e);
            }
            return pass;
        }

        private bool ValidateParameterEntry(DeploymentSyncParameterEntry parameterEntry)
        {
            bool pass = true;
            if (string.IsNullOrEmpty(parameterEntry.Match))
            {
                _parameterEntryMessages.Add("Match is Empty");
                return false;
            }

            if (string.IsNullOrEmpty(parameterEntry.Scope))
            {
                _parameterEntryMessages.Add("Scope is Empty");
                return false;
            }

            if (parameterEntry.Kind == DeploymentSyncParameterEntryKind.ProviderPath)
            {
                bool found = false;
                foreach (DeploymentObject child in Package.Current.MSDeployDeploymentObject.GetChildren())
                {
                    if (Regex.IsMatch(child.Name, parameterEntry.Scope, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
                    {
                        if (Regex.IsMatch(child.AbsolutePath, parameterEntry.Match, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        {
                            _parameterEntryMessages.Add(
                                "Scope " + parameterEntry.Scope + " matched provider entry " + child.Name + " in manifest");

                            _parameterEntryMessages.Add(
                                "Match " + parameterEntry.Match + " matched provider path " + child.AbsolutePath + " in manifest");

                            found = true;
                            break;
                        }
                    }

                    
                }

                if (!found)
                {
                    _parameterEntryMessages.Add(
                        "Match " + parameterEntry.Match + " did not match any provider path for " + parameterEntry.Scope + " in manifest");

                    pass = false;
                }
            }
            else if ((parameterEntry.Kind == DeploymentSyncParameterEntryKind.TextFile)
                  || (parameterEntry.Kind == DeploymentSyncParameterEntryKind.TextFilePosition)
                  || (parameterEntry.Kind == DeploymentSyncParameterEntryKind.XmlFile))
            {                
                foreach (ZipEntry zipEntry in Package.Current.ZipPackage)
                {
                    if (Regex.IsMatch(zipEntry.FileName, parameterEntry.Scope, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        _parameterEntryMessages.Add("File: " + zipEntry.FileName + " matches Scope:" + parameterEntry.Scope);

                        if (parameterEntry.Kind == DeploymentSyncParameterEntryKind.TextFile)
                        {
                            if (!VerifyTextPatternInFile(zipEntry.FileName, parameterEntry.Match))
                            {
                                pass = false;
                            }
                        }
                        else if (parameterEntry.Kind == DeploymentSyncParameterEntryKind.TextFilePosition)
                        {
                            _parameterEntryMessages.Add("TextFilePosition is deprecated, use TextFile with RegEx match instead");
                            pass = false;                         
                        }
                        else if (parameterEntry.Kind == DeploymentSyncParameterEntryKind.XmlFile)
                        {
                            if (!VerifyXPathExpressionInXmlFile(zipEntry.FileName, parameterEntry.Match))
                            {
                                pass = false;
                            }
                        }
                    }
                }
            }

            return pass;
        }        

        private bool VerifyTextPatternInFile(string filePath, string patternToMatch)
        {
            string text = Package.Current.GetZipFileContent(filePath);

            if (text.Contains(patternToMatch))
            {
                _parameterEntryMessages.Add("Literal String replacements will render application unusable after publish"
                + ", Absolute Regular Expressions should be used in TextFile match");
                return false;
            }

            if (Regex.IsMatch(text, patternToMatch, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                _parameterEntryMessages.Add("File: " + filePath + " contains Match: " + patternToMatch);
                return true;
            }
            else
            {
                _parameterEntryMessages.Add("File: " + filePath + " does not contain Match: " + patternToMatch);
                return false;
            }
        }

        private bool VerifyXPathExpressionInXmlFile(string filePath, string xPathExpression)
        {
            try
            {
                XPathExpression xPathMatch = XPathExpression.Compile(xPathExpression);
                XmlDocument document = new XmlDocument();
                document.Load(filePath);

                XPathNavigator navigator = document.CreateNavigator();
                XPathNodeIterator iterator = navigator.Select(xPathMatch);

                while (iterator.MoveNext())
                {
                    _parameterEntryMessages.Add("XPath expression " + xPathExpression + " matched in file");
                }

                return true;
            }
            catch (XPathException)
            {
                _parameterEntryMessages.Add("Invalid XPath expression " + xPathExpression);
                return false;
            }
            catch (ArgumentException)
            {
                _parameterEntryMessages.Add("XPath expression in " + xPathExpression + " did not match anything in file");
                return false;
            }
            catch (Exception)
            {
                _parameterEntryMessages.Add("XPath expression in " + xPathExpression + " did not match anything in file");
                return false;
            }
        }

     /*   private bool VerifyTextPositionInFile(string filePath, string positionInFile)
        {
            int line = 0;
            int column = 0;
            int countToReplace = 0;
            List<string> lines = ReadAllLinesInFile(filePath);
            
            if (!GetPositionalParameters(positionInFile, out line, out column, out countToReplace))
            {
                _parameterEntryMessages.Add("Match " + positionInFile + " is invalid");
                return false;
            }

            if (lines.Count < line)
            {
                _parameterEntryMessages.Add(
                    "Line number " + line.ToString() + " specified in" + positionInFile + " exceeds no. of lines in file");
                return false;
            }

            string lineToReplace = lines[line - 1];

            _parameterEntryMessages.Add("Line : " + lineToReplace);

            if (lineToReplace.Length < column)
            {
                _parameterEntryMessages.Add("Column number " + column + " specified in" + positionInFile + " exceeds length of line");
                return false;
            }

            if (lineToReplace.Length < column + countToReplace)
            {
                _parameterEntryMessages.Add("Count to replace " + countToReplace + " specified in" + positionInFile + " is beyond end of line");
                return false;
            }

            _parameterEntryMessages.Add("String: " + lineToReplace.Substring(column - 1, countToReplace) + " will be replaced");

            return true;
        }

        private List<string> ReadAllLinesInFile(string filePath)
        {
            List<string> allLines = new List<string>();
            using (MemoryStream memoryStream = Package.Current.GetZipFileMemoryStream(filePath))
            {
                using (StreamReader streamReader = new StreamReader(memoryStream))
                {
                    while (streamReader.Peek() >= 0)
                    {
                        allLines.Add(streamReader.ReadLine());
                    }
                }
            }
            return allLines;
        }       

        private bool GetPositionalParameters(string input,
            out int line,
            out int col,
            out int countToReplace)
        {
            line = 0;
            col = 0;
            countToReplace = 0;

            Match m = null;
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            m = Regex.Match(input, @"^\s*(\d+)\s*;\s*(\d+)\s*;\s*(\d+)\s*$");
            if (m == null || !m.Success || m.Groups.Count != 4)
            {   
                return false;
            }

            if (!int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out line)
               || !int.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out col)
               || !int.TryParse(m.Groups[3].Value, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out countToReplace))
            {
                return false;
            }

            if (line < 1 || col < 1 || countToReplace < 0)
            {
                return false;
            }

            return true;
        }*/
    }
}