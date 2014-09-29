using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;
using Microsoft.WebPlatform.Test.Logging;

using LogEventType = AppGallery.SIR.ILog.LogEvent.LogEventType;
using ValidationResult = AppGallery.SIR.ILog.ValidationResult;
using DatabaseChoice = AppGallery.SIR.Package.DatabaseChoice;

namespace AppGallery.SIR
{
    public class PackageValidationManager
    {
        #region Constants

        public static readonly string ExecutableFolder = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        public static readonly string TempFolderPath = Environment.ExpandEnvironmentVariables("%temp%");
        public static readonly string ParametersSchemaFileName = "Parameters.xsd";
        
        #endregion

        #region Data Members

        protected Sniffer _sniffer;
        protected Installer _installer;
        protected ILog _reporter;        
        protected bool _needToRemoveTempPackage = false;

        protected DbInfo _sqlInfo;
        protected DbInfo _mySqlInfo;

        protected string _sourcePath;        
        protected string _logFolder;
        protected string _unzippedFolderLocation = TempFolderPath;
        protected string _logFilePath;

        protected DatabaseChoice _dbChoice = DatabaseChoice.None;
        protected bool _skipInstallation = false;    
        protected bool _skipReportGeneration = false;
        
        protected static StreamReader _parametersXSDTextStreamReader;

        #endregion

        #region Properties

        public static StreamReader ParametersXSDTextStreamReader
        {
            get
            {
                return _parametersXSDTextStreamReader;
            }
            set
            {
                _parametersXSDTextStreamReader = value;
            }
        }

        public Sniffer Sniffer
        {
            get
            {
                if (_sniffer == null)
                {
                    _sniffer = new Sniffer(this);
                }
                return _sniffer;
            }
        }

        public Installer Installer
        {
            get
            {
                if (_installer == null)
                {
                    _installer = new Installer(this);
                }
                return _installer;
            }
        }
       
        public ILog Reporter
        {
            get
            {
                if (_reporter == null)
                {
                    _reporter = new Reporter(this);
                }
                return _reporter;
            }
        }

        public DatabaseChoice DbChoice
        {
            get { return _dbChoice; }
            set { _dbChoice = value; }
        }

        public DbInfo SqlDbInfo
        {
            get { return _sqlInfo; }
            set { _sqlInfo = value; }
        }

        public DbInfo MySqlDbInfo
        {
            get { return _mySqlInfo; }
            set { _mySqlInfo = value; }
        }

        public bool SkipInstallation
        {
            get { return _skipInstallation; }
            set { _skipInstallation = value; }
        }

        public bool SkipReportGeneration
        {
            get { return _skipReportGeneration; }
            set { _skipReportGeneration = value; }
        }

        public string SourcePath
        {
            get { return _sourcePath; }
            set
            {
                if (value != null)
                {
                    _sourcePath = Environment.ExpandEnvironmentVariables(value);
                }
            }
        }
        
        public string LogFilePath
        {
            get 
            {
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    int i = 1;
                    do 
                    {
                        _logFilePath = Path.Combine(LogFolder, string.Format("AppGallerySIR00{0}.log", i));
                        i++;
                    } while (File.Exists(_logFilePath));
                }
                return _logFilePath;
            }
            set { _logFilePath = value; }
        }

        public string LogFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_logFolder))
                {
                    _logFolder = ExecutableFolder;
                }
                return _logFolder;
            }
            set
            {
                if (!string.IsNullOrEmpty(value) && Directory.Exists(Environment.ExpandEnvironmentVariables(value)))
                {
                    _logFolder = Environment.ExpandEnvironmentVariables(value);
                }
            }
        }

        public string UnzippedFolderLocation
        {
            get { return _unzippedFolderLocation; }
            set
            {
                if (!string.IsNullOrEmpty(value) && Directory.Exists(Environment.ExpandEnvironmentVariables(value)))
                {
                    _unzippedFolderLocation = Environment.ExpandEnvironmentVariables(value);
                }
            }
        }

        #endregion

        #region Event and Delegate

        public delegate void ValidationStatusUpdatedHandler(object sender, StatusUpdatedEventArgs e);
        public event ValidationStatusUpdatedHandler ValidationStatusUpdated;

        public delegate void ValidationCompletedHandler(object sender, ValidationCompletedEventArgs e);
        public event ValidationCompletedHandler ValidationCompleted;

        #endregion

        #region Constructor

        public PackageValidationManager()
        {
            Assembly _assembly;
            _assembly = Assembly.GetExecutingAssembly();
            _parametersXSDTextStreamReader = new StreamReader(_assembly.GetManifestResourceStream("AppGallery.SIR.Parameters.xsd"));
        }

        #endregion

        #region Methods

        public bool CheckIfRequiredSettingsPresent(out string requiredSettingsString)
        {
            if (!string.IsNullOrEmpty(SourcePath))
            {
                if (Helper.IsAbsolutePhysicalPath(SourcePath))
                {
                    if (File.Exists(SourcePath) || Directory.Exists(SourcePath))
                    {
                        requiredSettingsString = string.Empty;
                        return true;
                    }
                    else
                    {
                        requiredSettingsString = string.Format("File {0} does not exist", SourcePath);
                        return false;
                    }
                }
                else if (Helper.IsUri(SourcePath))
                {
                    requiredSettingsString = string.Empty;
                    return true;
                }
                else 
                {
                    requiredSettingsString = string.Format("The source path to the package {0} is not valid", SourcePath);
                    return false;
                }
            }
            else 
            {
                requiredSettingsString = "The source path to the package is not set";
                return false;
            }
        }

        public int ValidatePackages(string folderPath)
        {
            // remove unnecesasry quotes
            folderPath = folderPath.Replace("\"", "");
            bool validationResult = true;

            string[] packages = Directory.GetFiles(folderPath, "*.zip");
            foreach (string package in packages)
            {
                if (ValidatePackage(package) != 0)
                {
                    validationResult = false;
                }
            }

            if (validationResult)
            {
                return 0;
            }
            else
            {
                return -1;
            }
        }

        public int ValidatePackage(string packagePath)
        {
            ValidationResult validationResult = ValidationResult.Unknown;
            // TODO: shouldnt this be LogFolder?
            SIRTraceListener.EnableTextFileTraceListener(LogFilePath);
            try
            {
                SubscribeForEvents();
                Console.WriteLine("\n\nValidating Package " + packagePath);
                try
                {
                    if (InitializePackage(packagePath))
                    {
                        Sniffer.ValidateDeploymentObject();
                        if (Sniffer.ReadZipPackage(packagePath))
                        {
                            if (!Sniffer.ValidateParametersFile())
                            {
                                validationResult = ValidationResult.Fail;
                            }
                            if (!Sniffer.ValidateParameterTags())
                            {
                                validationResult = ValidationResult.Fail;
                            }
                            if (!Sniffer.ValidateParameterEntries())
                            {
                                validationResult = ValidationResult.Fail;
                            }
                            if (Sniffer.ValidateManifestFileExists())
                            {
                                if (!Sniffer.ValidateProviders())
                                {
                                    validationResult = ValidationResult.Fail;
                                }

                                if (!Sniffer.ValidateManifestEntries())
                                {
                                    validationResult = ValidationResult.Fail;
                                }                                
                            }
                            else
                            {
                                validationResult = ValidationResult.Fail;
                            }
                        }
                        else
                        {
                            validationResult = ValidationResult.Fail;
                        }
                    }
                    else
                    {
                        validationResult = ValidationResult.Fail;
                    }

                    // No failures, reset flag from unknown to pass
                    if (validationResult != ValidationResult.Fail)
                    {
                        validationResult = ValidationResult.Pass;
                    }
                }
                catch (Exception e)
                {                    
                    Trace.TraceError(e.ToString() + e.StackTrace);
                    validationResult = ValidationResult.Fail;
                    Reporter.Log(LogEventType.Fail, "Unexpected error occurred. Exception: " + e.ToString());
                }
                // Log the result of pass/fail testing
                Reporter.LogResult(validationResult);

                if (validationResult == ValidationResult.Pass)
                {
                    Reporter.Log(LogEventType.Pass, "Package Validation successful");
                    Sniffer.DumpAcls();
                    InstallValidPackage();
                }
            }
            catch (Exception e)
            {
                Reporter.Log(LogEventType.Installation, "Installation failed");
                Trace.TraceError(e.ToString() + e.StackTrace);
            }
            finally
            {
                if (!SkipReportGeneration)
                {
                    Reporter.GenerateLog();

                    // Log to WTT if installed
                    if (MTFLogger.Initialize())
                    {
                        MTFLogger.GenerateLog(Reporter.Events);
                    }

                    Console.WriteLine("\nThe detailed evaluation report can be found at " + Reporter.ReportFileName);
                    Reporter.Clean();       
                }        
                if (_needToRemoveTempPackage)
                {
                    Helper.ForceFileDeletion(Package.Current.PackagePath);
                }

                if (ValidationCompleted != null)
                {
                    ValidationCompleted(this, new ValidationCompletedEventArgs(validationResult));
                }
                UnsubscribeFromEvents();

                if (Package.Current != null)
                {
                    Package.Current.Dispose();
                }
            }

            if (validationResult == ValidationResult.Pass)
            {
                return 0;
            }
            else
            {
                return -1;
            }
        }        

        public void InstallValidPackage()
        {
            if (SkipInstallation)
            {
                Reporter.Log(LogEventType.Installation, "Installation was configured to be skipped");
            }
            else
            {
                if (!Helper.IsIISInstalled())
                {                    
                    Reporter.Log(LogEventType.Fail, "IIS is not installed, cannot proceed with package installation");
                }
                else if (!Helper.IsMsDeployInstalled())
                {
                    Reporter.Log(LogEventType.Fail, "MSDeploy is not installed, cannot proceed with package installation");
                }
                else
                {
                    Console.WriteLine("IIS " + Helper.IISMajorVersion.ToString() + "." + Helper.IISMinorVersion.ToString() + " is installed");

                    // Support package installation on IIS 5.1 or above
                    if (Helper.IISMajorVersion <= 5 && Helper.IISMinorVersion < 1)
                    {
                        Reporter.Log(LogEventType.Informational, "IIS is not installed, cannot proceed with package installation");
                    }
                    else
                    {
                        Console.WriteLine(Helper.FormattedTimeStampString + "Installing the package...");
                        Installer.Install();
                    }
                }
            }
            Installer.LogCommandLine();
        }

        private bool InitializePackage(string packagePath)
        {
            string packagePhysicalPath;
            // the path should be either original URL or physical path
            Reporter.PackageLocation = packagePath;
            if (Helper.IsAbsolutePhysicalPath(packagePath))
            {
                packagePhysicalPath = packagePath;
            }
            else if (Helper.IsUri(packagePath))
            {
                packagePhysicalPath = Path.Combine(TempFolderPath, "downloadedFile_" + Helper.GetTimeStampString(DateTime.Now) + ".zip");
                Console.WriteLine(Helper.FormattedTimeStampString + "Downloading package " + packagePath);
                WPIWebClient wpiWebClient = new WPIWebClient(this);
                wpiWebClient.DownloadFile(packagePath, packagePhysicalPath);
                Console.WriteLine(Helper.FormattedTimeStampString + "Successfully downloaded the package");
                _needToRemoveTempPackage = true;
            }
            else
            {
                Console.WriteLine("Please specify an absolute path to the package");
                return false;
            }
            Package.Create(packagePhysicalPath);

            return true;
        }

        private void SubscribeForEvents()
        {
            Reporter.StatusUpdated += new ILog.StatusUpdatedHandler(Reporter_StatusUpdated);
        }

        private void UnsubscribeFromEvents()
        {
            Reporter.StatusUpdated -= new ILog.StatusUpdatedHandler(Reporter_StatusUpdated);
        }

        private void Reporter_StatusUpdated(object sender, StatusUpdatedEventArgs e)
        {
            if (ValidationStatusUpdated != null)
            {
                ValidationStatusUpdated(this, e);
            }
        }

        #endregion
    }
}