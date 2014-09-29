//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Diagnostics;

namespace Microsoft.WindowsAzure.Management.Marketplace.Rest.WebAppGallery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Web.Script.Serialization;
    using Microsoft.Web.Deployment;
    using Microsoft.Web.PlatformInstaller;
    using System.Globalization;
    using System.Collections.ObjectModel;

    internal static class WebDeployPackageParametersTranslator
    {
        private static object syncLock = new object();

        public static string GetParametersJson(Product product, Installer installer)
        {
            string packageFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
            DeploymentObject iisApplication = null;
            try
            {
                DownloadPackageToTempLocation(installer.InstallerFile.InstallerUrl, installer.InstallerFile.SHA1Hash, packageFilePath);

                // Extract all parameters which require user input
                PackageParameter[] parametersArray = null;
                int databaseType = 0; /* none */

                try
                {
                    lock (syncLock)
                    {
                        // Try to create a package object - If this fails, the package is invalid - we typically do not get useful information about it
                        iisApplication = DeploymentManager.CreateObject(DeploymentWellKnownProvider.Package, packageFilePath);
                    }
                }
                catch(Exception ex)
                {
                    // Unwind the exception to get to the root cause
                    string collatedErrorMessage = String.Format(CultureInfo.CurrentUICulture,
                                                                "Product '{0}' - Validation of package '{1}' failed.\n", installer.Product.Title, installer.InstallerFile.InstallerUrl.ToString());
                    Exception innerException = ex.InnerException;
                    while (innerException != null)
                    {
                        collatedErrorMessage = collatedErrorMessage + String.Format(CultureInfo.CurrentUICulture, "{0}\n", innerException.Message);
                        innerException = innerException.InnerException;
                    }

                    throw new InvalidDataException(collatedErrorMessage, ex);
                }

                // Convert parameters for future use
                List<PackageParameter> packageParameters = new List<PackageParameter>();
                if (iisApplication.SyncParameters != null)
                {
                    foreach (DeploymentSyncParameter syncParameter in iisApplication.SyncParameters)
                    {
                        bool isEnumeration;
                        string[] enumerationValues;
                        GetEnumerationStatus(syncParameter, out isEnumeration, out enumerationValues);

                        PackageParameter param = new PackageParameter()
                        {
                            Name = syncParameter.Name,
                            Description = syncParameter.Description,
                            IsPassword = IsPassword(syncParameter),
                            IsHidden = IsHidden(syncParameter),
                            IsApplicationPath = IsApplicationPath(syncParameter),
                            IsAppUrl = IsAppUrl(syncParameter),
                            IsEnumeration = isEnumeration,
                            ValidationValues = isEnumeration ? enumerationValues : SplitValidationValues(syncParameter),
                            IsDbParameter = IsDatabaseParameter(syncParameter),
                            AllowEmpty = AllowEmpty(syncParameter),
                            RegEx = HasRegEx(syncParameter) ? syncParameter.Validation.ValidationString : null,
                            Value = syncParameter.DefaultValue,
                            Tags = SplitTagsToUppercase(syncParameter),
                            WellKnownTags = (long)syncParameter.WellKnownTags
                        };

                        packageParameters.Add(param);
                    }
                }

                parametersArray = packageParameters.ToArray();
                // Determine database type
                databaseType = DetermineDatabaseType(product.Keywords, iisApplication.SyncParameters);

                // Create an anonymous type to hold all the data we need
                var packageProperties = new
                {
                    packageDownloadUrl = installer.InstallerFile.InstallerUrl,
                    cacheDownloadUrl = string.Empty,
                    trackingUrl = installer.InstallerFile.TrackingUrl,
                    startPage = string.IsNullOrEmpty(installer.MSDeployPackage.StartPage) ? string.Empty : installer.MSDeployPackage.StartPage,
                    sha1 = installer.InstallerFile.SHA1Hash,
                    database = databaseType,
                    uiParameters = parametersArray,
                    dependencies = GetDependenciesAsCommaSeparatedString(product),
                    useIntegratedSqlScript = UseIntegratedSqlScript(product)
                };

                // Serialize results
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                return serializer.Serialize(packageProperties);
            }
            finally
            {
                // Release the deployment object
                if (iisApplication != null)
                {
                    iisApplication.Dispose();
                    iisApplication = null;
                }

                // Attempt to delete the temporary package
                try
                {
                    if (File.Exists(packageFilePath))
                    {
                        File.Delete(packageFilePath);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        // Use an agreed-upon keyword to indicate that the package has been updated to use a new SQL script format.
        // This is a future-proof plan so we won't need to issue an QFE when/if WebDeploy update the guidelines to have the CreateLogon/CreateUser scripts idempotent.
        private static bool UseIntegratedSqlScript(Product product)
        {
            return product.Keywords.Any(k => k.Text.Equals("UseIntegratedSqlScript", StringComparison.OrdinalIgnoreCase));
        }

        private static string[] GetDependenciesAsCommaSeparatedString(Product product)
        {
            if (product.DependencySets != null)
            {
                // Iterate through all dependency sets
                HashSet<string> detectedDotNetDependencies = new HashSet<string>();
                foreach (ReadOnlyCollection<Product> theDependencySet in product.DependencySets)
                {
                    ////
                    // We are currently only interested in the version of .NET that is suitable for the application
                    // We collect all supported versions
                    ////
                    Product dotNetDependency = theDependencySet.SingleOrDefault(entry => entry.ProductId.StartsWith("NETFramework", StringComparison.OrdinalIgnoreCase));
                    if ((dotNetDependency != null) && !detectedDotNetDependencies.Contains(dotNetDependency.ProductId))
                    {
                        detectedDotNetDependencies.Add(dotNetDependency.ProductId);
                    }
                }

                return detectedDotNetDependencies.ToArray();
            }
            else
            {
                return new string[0];
            }
        }

        private static bool IsDatabaseParameter(DeploymentSyncParameter parameter)
        {
            return (((parameter.WellKnownTags & DeploymentWellKnownTag.Sql) == DeploymentWellKnownTag.Sql) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.MySql) == DeploymentWellKnownTag.MySql) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.SqlCE) == DeploymentWellKnownTag.SqlCE) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.SqLite) == DeploymentWellKnownTag.SqLite)) &&

                   // We care only about those parameters which have *both* a database flavor as well as a DbXXX tag
                   (((parameter.WellKnownTags & DeploymentWellKnownTag.DBAdminPassword) == DeploymentWellKnownTag.DBAdminPassword) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBAdminUserName) == DeploymentWellKnownTag.DBAdminUserName) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBConnectionString) == DeploymentWellKnownTag.DBConnectionString) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBName) == DeploymentWellKnownTag.DBName) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBServer) == DeploymentWellKnownTag.DBServer) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBUserName) == DeploymentWellKnownTag.DBUserName) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBUserPassword) == DeploymentWellKnownTag.DBUserPassword) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.MySqlConnectionString) == DeploymentWellKnownTag.MySqlConnectionString) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.SqlConnectionString) == DeploymentWellKnownTag.SqlConnectionString) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBUserConnectionString) == DeploymentWellKnownTag.DBUserConnectionString) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBAdminConnectionString) == DeploymentWellKnownTag.DBAdminConnectionString));
        }

        private static int DetermineDatabaseType(ReadOnlyCollection<Keyword> feedKeywords, DeploymentSyncParameterCollection deploymentSyncParameterCollection)
        {
            int databaseType = 0; /* none */

            // Locate all database related parameters
            List<DeploymentSyncParameter> databaseParameters = (from syncParameter in deploymentSyncParameterCollection
                                                                where IsDatabaseParameter(syncParameter)
                                                                select syncParameter).ToList();
            if (databaseParameters.Count > 0)
            {
                bool supportSqlServer = databaseParameters.Count<DeploymentSyncParameter>(parameter => (parameter.WellKnownTags & DeploymentWellKnownTag.Sql) == DeploymentWellKnownTag.Sql) > 0;
                bool supportMySql = databaseParameters.Count<DeploymentSyncParameter>(parameter => (parameter.WellKnownTags & DeploymentWellKnownTag.MySql) == DeploymentWellKnownTag.MySql) > 0;

                // Now, that we know what the app supports, get the feed keywords - A database technology can only be used when both the app supports it and the ffed has the keyword
                bool feedAllowsSqlServer = feedKeywords.Count<Keyword>(keyword => string.CompareOrdinal(keyword.Id, WellKnownKeywords.SqlKeyword) == 0) > 0;
                bool feedALlowsMySQL = feedKeywords.Count<Keyword>(keyword => string.CompareOrdinal(keyword.Id, WellKnownKeywords.MySqlKeyword) == 0) > 0;

                if ((supportSqlServer && feedAllowsSqlServer) && (supportMySql && feedALlowsMySQL))
                {
                    databaseType = 3; /* Both */
                }
                else if (supportSqlServer && feedAllowsSqlServer)
                {
                    databaseType = 1; /* SQL Server */
                }
                else if (supportMySql && feedALlowsMySQL)
                {
                    databaseType = 2; /* mySQL */
                }
            }

            return databaseType;
        }

        private static void DownloadPackageToTempLocation(Uri packageUri, string expectedSha1, string destinationPath)
        {
            // Fetch the file
            try
            {
                HttpWebRequest httpWebRequest = HttpWebRequest.Create(packageUri) as HttpWebRequest;
                httpWebRequest.Method = "GET";
                httpWebRequest.UserAgent = "Platform-Installer/3.0.3.0(" + Environment.OSVersion.VersionString + ")";
                httpWebRequest.Timeout = (int) (new TimeSpan(0, 0, 3, 0)).TotalMilliseconds;
                httpWebRequest.ReadWriteTimeout = (int) (new TimeSpan(0, 0, 3, 0)).TotalMilliseconds;

                using (HttpWebResponse httpWebResponse = httpWebRequest.GetResponse() as HttpWebResponse)
                {
                    using (Stream responseStream = httpWebResponse.GetResponseStream())
                    {
                        using (FileStream fileStream = new FileStream(destinationPath, FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            responseStream.CopyTo(fileStream);
                        }
                    }
                } 
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentUICulture, "Could not download package '{0}'. Error message : {1}.", packageUri, ex.Message), ex);
            }

            // Verify the SHA1
            byte[] hash;
            using (SHA1 sha1 = SHA1.Create())
            {
                hash = sha1.ComputeHash(File.ReadAllBytes(destinationPath));
            }

            string hashString = hash.Aggregate(string.Empty, (acc, value) => acc + value.ToString("X2"));
            if (expectedSha1.ToUpperInvariant() != hashString)
            {
                File.Delete(destinationPath);
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentUICulture, "Package '{0}' appears to have an invalid SHA1. Feed SHA1 '{1}' - Actual SHA1: '{2}'", packageUri, expectedSha1.ToUpperInvariant(), hashString));
            }
        }

        private static bool IsApplicationPath(DeploymentSyncParameter parameter)
        {
            return SplitTagsToUppercase(parameter).Any(t => t == "IISAPP");
        }

        private static bool IsAppUrl(DeploymentSyncParameter parameter)
        {
            return SplitTagsToUppercase(parameter).Any(t => t == "APPURL");
        }

        private static bool IsHidden(DeploymentSyncParameter parameter)
        {
            return SplitTagsToUppercase(parameter).Any(t => t == "HIDDEN");
        }

        private static bool IsPassword(DeploymentSyncParameter parameter)
        {
            return SplitTagsToUppercase(parameter).Any(t => t == "PASSWORD");
        }

        private static void GetEnumerationStatus(DeploymentSyncParameter parameter, out bool isEnumeration, out string[] enumerationValues)
        {
            isEnumeration = false;
            enumerationValues = new string[0];

            string[] validationValues = SplitValidationValues(parameter);

            // It can only be an enumeration if it has validation values
            if ((validationValues != null) && (validationValues.Length > 0))
            {
                ////
                // Heuristic: We consider a parameter to be an enumeration if one of the following is true:
                //  * Its Validation.Kind is "Enumeration"
                //  * Its Validation.Kind is "Boolean"
                //  * Its Validation.Kind is NOT "RegularExpression" and it has exactly two validation values being exactly "True", "False"
                ////
                bool isBoolean = ((parameter.Validation.Kind & DeploymentSyncParameterValidationKind.Boolean) == DeploymentSyncParameterValidationKind.Boolean) ||
                                 (((parameter.Validation.Kind & DeploymentSyncParameterValidationKind.RegularExpression) != DeploymentSyncParameterValidationKind.RegularExpression) &&
                                  ((validationValues.Length == 2) && validationValues.Contains<string>("True") && validationValues.Contains<string>("False")));
                bool isMarkedAsEnumeration = (parameter.Validation.Kind & DeploymentSyncParameterValidationKind.Enumeration) == DeploymentSyncParameterValidationKind.Enumeration;

                // Are we in presence of an enumeration
                isEnumeration = isBoolean || isMarkedAsEnumeration;
                if (isEnumeration)
                {
                    // If we have a boolean, specify "True" / "False" as the value
                    if (isBoolean)
                    {
                        enumerationValues = new string[] { "True", "False" };
                    }
                    else
                    {
                        enumerationValues = validationValues;
                    }
                }
            }
        }

        private static bool HasRegEx(DeploymentSyncParameter parameter)
        {
            if (parameter.Validation == null)
            {
                return false;
            }

            return (parameter.Validation.Kind & DeploymentSyncParameterValidationKind.RegularExpression) == DeploymentSyncParameterValidationKind.RegularExpression;
        }

        private static bool AllowEmpty(DeploymentSyncParameter parameter)
        {
            if (parameter.Validation == null)
            {
                return false;
            }

            return (parameter.Validation.Kind & DeploymentSyncParameterValidationKind.AllowEmpty) == DeploymentSyncParameterValidationKind.AllowEmpty;
        }

        private static string[] SplitTagsToUppercase(DeploymentSyncParameter parameter)
        {
            if (parameter.Tags == null)
            {
                return new string[0];
            }

            return parameter.Tags.ToUpperInvariant().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string[] SplitValidationValues(DeploymentSyncParameter parameter)
        {
            if (parameter.Validation.ValidationString == null)
            {
                return new string[0];
            }

            return parameter.Validation.ValidationString.Split(new[] { ',', ' ' }, StringSplitOptions.None);
        }
    }
}
