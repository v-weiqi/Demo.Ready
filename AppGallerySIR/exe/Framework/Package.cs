using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

using Ionic.Zip;

using Microsoft.Web.Deployment;

namespace AppGallery.SIR
{
    public class Package : IDisposable
    {
        public enum DatabaseChoice
        {
            SQL,
            MySQL,
            Both,
            None,
            Unknown
        }

        #region Data Members

        private static Package _currentPackage;
        private DeploymentObject _deploymentObject;
        private DeploymentBaseOptions _deploymentBaseOptions;
        private List<DeploymentObject> _providers;
        private string _packagePath;        
        private string _parametersFile = "parameters.xml";
        private string _manifestFile = "manifest.xml";
        private string _packageFriendlyName;
        private string _md5Hash;
        private string _sha1Hash;
        private string _sha512Hash;
        private string _applicationUrl;
        private string _applicationInstallPath;
        private string _applicationName;
        private ZipFile _zipPackage;
        private DatabaseChoice _dbChoice = DatabaseChoice.None;
        private MemoryStream _parametersMemoryStream;

        #endregion

        #region Properties

        private Package(string packagePath) 
        { 
            this._packagePath = packagePath;
        }

        public static Package Current
        {
            get { return _currentPackage; }
        }

        public string PackagePath
        {
            get { return _packagePath; }
        }

        public string PackageFriendlyName
        {
            get
            {
                if (Package.Current != null && _packageFriendlyName == null)
                {
                    _packageFriendlyName = Path.GetFileNameWithoutExtension(PackagePath);
                }
                return _packageFriendlyName;
            }
        }

        public string ParametersFile
        {
            get { return _parametersFile; }
        }

        public ZipFile ZipPackage
        {
            get { return _zipPackage; }
        }

        public MemoryStream ParametersMemoryStream
        {
            get
            {
                if (_parametersMemoryStream == null)
                {
                    _parametersMemoryStream = GetZipFileMemoryStream(_parametersFile);
                }
                if (_parametersMemoryStream != null)
                {
                    _parametersMemoryStream.Seek(0, SeekOrigin.Begin);
                }
                return _parametersMemoryStream;
            }
        }

        public DatabaseChoice DbChoice
        {
            get { return _dbChoice; }
            set 
            { 
                _dbChoice = value;

                if (_dbChoice == DatabaseChoice.SQL || _dbChoice == DatabaseChoice.MySQL)
                {
                    DeploymentSkipDirective directive;
                    if (DbChoice.Equals(DatabaseChoice.MySQL))
                    {
                        directive = new DeploymentSkipDirective("SkipDatabase", "objectName=dbFullSql");
                    }
                    else
                    {
                        directive = new DeploymentSkipDirective("SkipDatabase", "objectName=dbMySql");
                    }
                    BaseOptions.SkipDirectives.Add(directive);
                    CreateDeploymentObject();
                }
            }
        }

        public DeploymentBaseOptions BaseOptions
        {
            get
            {
                if (_deploymentBaseOptions == null)
                {
                    _deploymentBaseOptions = new DeploymentBaseOptions();
                    _deploymentBaseOptions.TraceLevel = TraceLevel.Verbose | TraceLevel.Info | TraceLevel.Error | TraceLevel.Warning;
                    _deploymentBaseOptions.Trace += Helper.TraceEventHandler;
                }
                return _deploymentBaseOptions;
            }
        }

        public DeploymentObject MSDeployDeploymentObject
        {
            get { return _deploymentObject; }
        }

        public List<DeploymentObject> Providers
        {
            get
            {
                if (_providers == null)
                {
                    _providers = new List<DeploymentObject>();
                    IEnumerable<DeploymentObject> children = MSDeployDeploymentObject.GetChildren();
                    foreach (DeploymentObject child in children)
                    {
                        _providers.Add(child);
                    }
                }
                return _providers;
            }
        }

        public string MD5Hash
        {
            get
            {
                if (string.IsNullOrEmpty(_md5Hash))
                {                  
                   _md5Hash = System.BitConverter.ToString((new MD5CryptoServiceProvider()).ComputeHash(File.ReadAllBytes(PackagePath)));
                   _md5Hash = _md5Hash.Replace("-", "");
                }
                return _md5Hash;
            }
        }

        public string SHA1Hash
        {
            get
            {
                if (string.IsNullOrEmpty(_sha1Hash))
                {
                    _sha1Hash = System.BitConverter.ToString((new SHA1CryptoServiceProvider()).ComputeHash(File.ReadAllBytes(PackagePath)));
                    _sha1Hash = _sha1Hash.Replace("-", "");
                }
                return _sha1Hash;
            }
        }

        public string SHA512Hash
        {
            get
            {
                if (string.IsNullOrEmpty(_sha512Hash))
                {
                    _sha512Hash = _sha1Hash = System.BitConverter.ToString((new SHA512Managed()).ComputeHash(File.ReadAllBytes(PackagePath)));
                    _sha512Hash = _sha512Hash.Replace("-", "");
                }
                return _sha512Hash;
            }
        }

        public bool ContainsSql
        {
            get
            {
               return Package.Current.ContainsProvider(DeploymentWellKnownProvider.DBFullSql);
            }
        }

        public bool ContainsMySql
        {
            get 
            {
                return Package.Current.ContainsProvider(DeploymentWellKnownProvider.DBMySql);
            }
        }

        public string ApplicationUrl
        {
            get { return _applicationUrl; }
            set { _applicationUrl = value; }
        }

        public string ApplicationInstallPath
        {
            get { return _applicationInstallPath; }
            set { _applicationInstallPath = value; }
        }

        public string ApplicationName
        {
            get
            {
                if (string.IsNullOrEmpty(_applicationName))
                {
                    _applicationName = Package.Current.PackageFriendlyName;
                }
                return _applicationName;
            }
            set { _applicationName = value; }
        }
        
        #endregion

        #region Methods

        public virtual void Dispose()
        {
            if (_parametersMemoryStream != null)
            {
                _parametersMemoryStream.Dispose();
                _parametersMemoryStream = null;
            }
            if (_deploymentObject != null)
            {
                _deploymentObject.Dispose();
                _deploymentObject = null;
            }
            if (_zipPackage != null)
            {
                _zipPackage.Dispose();
                _zipPackage = null;
            }
        }
        
        public static Package Create(string packagePath)
        {
            _currentPackage = new Package(packagePath);
            return _currentPackage;
        }

        public void CreateDeploymentObject()
        {
            _deploymentObject = DeploymentManager.CreateObject
            (
                DeploymentWellKnownProvider.Package,
                PackagePath,
                BaseOptions
             );
        }

        public void CreateZipPackage()
        {
            _zipPackage = new ZipFile(PackagePath);
        }

        public string GetZipFileContent(string fileName)
        {
            using (MemoryStream memoryStream = GetZipFileMemoryStream(fileName))
            {
                using (StreamReader streamReader = new StreamReader(memoryStream))
                {
                    if (streamReader != null)
                    {
                        return streamReader.ReadToEnd();
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        private MemoryStream GetZipFileMemoryStream(string fileName)
        {
            foreach (ZipEntry zipEntry in ZipPackage)
            {
                if (zipEntry.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    MemoryStream memoryStream = new MemoryStream();
                    zipEntry.Extract(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    return memoryStream;
                }
            }
            return null;
        }

        public bool DoesFileExist(string fileName)
        {
            bool fileExists = false;
            foreach (ZipEntry zipEntry in ZipPackage.Entries)
            {
                if (zipEntry.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                    zipEntry.FileName.Equals(fileName + @"/", StringComparison.OrdinalIgnoreCase))
                {
                    fileExists = true;
                    break;
                }
            }
            return fileExists;
        }

        public bool DoesManifestFileExist()
        {
            return DoesFileExist(_manifestFile);
        }

        public bool DoesParametersFileExist()
        {
            return DoesFileExist(_parametersFile);
        }

        public bool ContainsProvider(string providerName)
        {
            foreach (DeploymentObject provider in Providers)
            {
                if (provider.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsProvider(DeploymentWellKnownProvider wellKnownProvider)
        {
            return ContainsProvider(wellKnownProvider.ToString());
        }

        public List<DeploymentObject> GetProvidersByName(string providerName)
        {
            List<DeploymentObject> foundProviders = new List<DeploymentObject>();
            foreach (DeploymentObject provider in Providers)
            {
                if (provider.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase))
                {
                    foundProviders.Add(provider);
                }
            }
            return foundProviders;
        }

        public List<DeploymentObject> GetNonSpecifiedProviders(List<string> specifiedProviders)
        {
            List<DeploymentObject> nonSpecifiedProviders = new List<DeploymentObject>(Providers);
            foreach (DeploymentObject provider in Providers)
            {
                foreach (string specifiedProvider in specifiedProviders)
                {
                    if (provider.Name.Equals(specifiedProvider, StringComparison.OrdinalIgnoreCase))
                    {
                        if (nonSpecifiedProviders.Contains(provider))
                        {
                            nonSpecifiedProviders.Remove(provider);
                        }
                        break;
                    }
                }
            }
            return nonSpecifiedProviders;
        }

        #endregion
    }
}
