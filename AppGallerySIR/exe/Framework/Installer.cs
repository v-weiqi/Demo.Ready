using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Web.Deployment;

using DatabaseChoice = AppGallery.SIR.Package.DatabaseChoice;
using LogEventType = AppGallery.SIR.ILog.LogEvent.LogEventType;

namespace AppGallery.SIR
{
    public class Installer
    {
        #region Constants

        public const string PASSWORD = "password";
        public const string PASSWORD_MASK = "******";
        public const string WEBSITE = "Default Web Site";
        private static string FullMSDeployPath = Environment.ExpandEnvironmentVariables
            (@"%programfiles%\IIS\Microsoft Web Deploy\msdeploy.exe");
        private readonly char[] AppPathSeparators = new char[] { '\\', '/' };
        private WPIWebClient _webClient;

        #endregion

        #region Data Members

        private List<DeploymentSyncParameter> _removeParametersList = new List<DeploymentSyncParameter>();
        protected PackageValidationManager _packageValidationManager;

        #endregion

        public Installer(PackageValidationManager packageValidationManager)
        {
            _packageValidationManager = packageValidationManager;
        }

        public WPIWebClient WebClient
        {
            get
            {
                if (_webClient == null)
                {
                    _webClient = new WPIWebClient(_packageValidationManager);
                }
                return _webClient;
            }
        }

        #region Methods

        public void Install()
        {
            try
            {
                SetDatabaseChoice();
                SetParameters();
                RemoveUnnecessaryParameters();
                DeployPackage();
                VerifySuccessfullInstallation();
            }
            catch (Exception e)
            {
                _packageValidationManager.Reporter.Log(LogEventType.Fail, "The Application was unable to be installed.");
                _packageValidationManager.Reporter.Log(e);
            }
        }

        public void SetDatabaseChoice()
        {
            // if the package contains Sql provider, but not MySql, set the choice to Sql
            if (Package.Current.ContainsSql && !Package.Current.ContainsMySql)
            {
                Package.Current.DbChoice = DatabaseChoice.SQL;
            }
            // if the package contains MySql provider, but not Sql, set the choice to MySql
            else if (Package.Current.ContainsMySql && !Package.Current.ContainsSql)
            {
                Package.Current.DbChoice = DatabaseChoice.MySQL;
            }

            else if (Package.Current.ContainsSql && Package.Current.ContainsMySql)
            {
                if (_packageValidationManager.DbChoice == Package.DatabaseChoice.SQL || _packageValidationManager.DbChoice == DatabaseChoice.MySQL)
                {
                    Package.Current.DbChoice = _packageValidationManager.DbChoice;
                }
                else
                {
                    Package.Current.DbChoice = DatabaseChoice.SQL;
                }
            }
        }

        public void SetParameters()
        {
            foreach (DeploymentSyncParameter syncParameter in Package.Current.MSDeployDeploymentObject.SyncParameters)
            {
                // 1. Check if parameter should be removed
                if ((Package.Current.DbChoice == DatabaseChoice.MySQL && IsSqlOnlyTag(syncParameter)) ||
                    (Package.Current.DbChoice == DatabaseChoice.SQL && IsMySqlOnlyTag(syncParameter)))
                {
                    _removeParametersList.Add(syncParameter);
                }
                else if (IsIisAppParameter(syncParameter))
                {
                    if (ParameterHasDefaultValue(syncParameter))
                    {
                        int lastIndexOfSlash = syncParameter.DefaultValue.LastIndexOfAny(AppPathSeparators);
                        Package.Current.ApplicationName = (lastIndexOfSlash > 0) ? syncParameter.DefaultValue.Substring(lastIndexOfSlash + 1, syncParameter.DefaultValue.Length - lastIndexOfSlash - 1) : "";
                    }
                    else
                    {
                        syncParameter.Value = WEBSITE + "/" + Package.Current.ApplicationName;
                    }
                    Package.Current.ApplicationInstallPath = syncParameter.Value;
                }
                // set all parameters that 
                // 1. have no default value
                // 2. not hidden
                // Feed everything that is not tagged with random strings,
                // be smart about everything else
                else if (!ParameterHasDefaultValue(syncParameter) && !IsHiddenParameter(syncParameter))
                {
                    // if it is one of the pre-set db tags and we can set it
                    if (IsOneOfPresetDbTag(syncParameter) &&
                        ((Package.Current.DbChoice == DatabaseChoice.SQL && _packageValidationManager.SqlDbInfo != null) ||
                         (Package.Current.DbChoice == DatabaseChoice.MySQL && _packageValidationManager.MySqlDbInfo != null)))
                    {
                        if ((syncParameter.WellKnownTags & DeploymentWellKnownTag.DBAdminUserName) != 0)
                        {
                            switch (Package.Current.DbChoice)
                            {
                                case DatabaseChoice.SQL:
                                    syncParameter.Value = _packageValidationManager.SqlDbInfo.AdminUsername;
                                    break;
                                case DatabaseChoice.MySQL:
                                    syncParameter.Value = _packageValidationManager.MySqlDbInfo.AdminUsername;
                                    break;
                            }
                        }
                        else if ((syncParameter.WellKnownTags & DeploymentWellKnownTag.DBAdminPassword) != 0)
                        {
                            switch (Package.Current.DbChoice)
                            {
                                case DatabaseChoice.SQL:
                                    syncParameter.Value = _packageValidationManager.SqlDbInfo.AdminPassword;
                                    break;
                                case DatabaseChoice.MySQL:
                                    syncParameter.Value = _packageValidationManager.MySqlDbInfo.AdminPassword;
                                    break;
                            }
                        }
                        else if (((syncParameter.WellKnownTags & DeploymentWellKnownTag.DBServer) != 0))
                        {
                            switch (Package.Current.DbChoice)
                            {
                                case DatabaseChoice.SQL:
                                    syncParameter.Value =
                                        _packageValidationManager.SqlDbInfo.DbServer;
                                    break;
                                case DatabaseChoice.MySQL:
                                    syncParameter.Value =
                                        _packageValidationManager.MySqlDbInfo.DbServer;
                                    break;
                            }
                        }
                    }
                    else if ((syncParameter.WellKnownTags & DeploymentWellKnownTag.Password) != 0 ||
                             (syncParameter.WellKnownTags & DeploymentWellKnownTag.New) != 0)
                    {
                        syncParameter.Value = Helper.GetRandomStrongPassword();
                    }
                    else
                    {
                        //syncParameter.Value = Helper.GetRandomString();
                        syncParameter.Value = GenerateParameterValue(syncParameter.Validation);
                    }
                }
            }
        }

        private void RemoveUnnecessaryParameters()
        {
            foreach (DeploymentSyncParameter removeParameter in _removeParametersList)
            {
                Package.Current.MSDeployDeploymentObject.SyncParameters.Remove(removeParameter.Name);
            }
        }

        private void DeployPackage()
        {
            DeploymentBaseOptions baseOptions = new DeploymentBaseOptions();
            baseOptions.TraceLevel = TraceLevel.Info | TraceLevel.Error | TraceLevel.Warning;
            baseOptions.Trace += Helper.TraceEventHandler;

            Package.Current.MSDeployDeploymentObject.SyncTo
                (
                    DeploymentWellKnownProvider.Auto,
                    "",
                    baseOptions,
                    new DeploymentSyncOptions()
                 );

            _packageValidationManager.Reporter.Log(LogEventType.Pass, "The Application was successfully installed to " + Package.Current.ApplicationInstallPath);
            Package.Current.ApplicationUrl = "http://localhost/" + Package.Current.ApplicationName;
            _packageValidationManager.Reporter.AppUrl = Package.Current.ApplicationUrl;
        }

        private void VerifySuccessfullInstallation()
        {
            WebClient.SendRetryRequest(Package.Current.ApplicationUrl, 5);

            if (WPIWebClient.StatusCode >= 200 && WPIWebClient.StatusCode <= 399)
            {
                _packageValidationManager.Reporter.Log(LogEventType.Pass, "Success status code received: " + WPIWebClient.StatusCode);
            }
            else
            {
                _packageValidationManager.Reporter.Log(LogEventType.Fail, "Failure status code received: " + WPIWebClient.StatusCode);
            }
        }

        public string GenerateCommandLine()
        {
            string commandLine = "\"" + FullMSDeployPath + "\" -verb:sync " +
                                 "-source:package=\"" + Package.Current.PackagePath + "\" " +
                                 "-dest:auto ";
            foreach (DeploymentSyncParameter syncParameter in Package.Current.MSDeployDeploymentObject.SyncParameters)
            {
                if (!IsHiddenParameter(syncParameter))
                {
                    commandLine += "-setParam:name=\"" + syncParameter.Name + "\",value=\"" +
                        ((syncParameter.Name.ToLower().Contains(PASSWORD) || syncParameter.Tags.ToLower().Contains(PASSWORD)) ? PASSWORD_MASK : syncParameter.Value) + "\" ";
                }
            }

            if (Package.Current.ContainsSql && Package.Current.ContainsMySql)
            {
                if (Package.Current.DbChoice == DatabaseChoice.SQL)
                {
                    commandLine += "-skip:objectName=dbMySql";
                }
                else if (Package.Current.DbChoice == DatabaseChoice.MySQL)
                {
                    commandLine += "-skip:objectName=dbFullSql";
                }
            }
            return commandLine;
        }

        public void LogCommandLine()
        {
            _packageValidationManager.Reporter.Log(LogEventType.Installation, GenerateCommandLine());
        }

        #endregion

        #region Helpers

        public bool IsIisAppParameter(DeploymentSyncParameter parameter)
        {
            return ((parameter.WellKnownTags & DeploymentWellKnownTag.IisApp) != 0);
        }

        public bool IsHiddenParameter(DeploymentSyncParameter parameter)
        {
            return ((parameter.WellKnownTags & DeploymentWellKnownTag.Hidden) != 0);
        }

        public bool IsOneOfPresetDbTag(DeploymentSyncParameter parameter)
        {
            return (((parameter.WellKnownTags & DeploymentWellKnownTag.DBAdminUserName) != 0) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBAdminPassword) != 0) ||
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.DBServer) != 0));
        }

        public bool IsMySqlOnlyTag(DeploymentSyncParameter parameter)
        {
            // it should be EITHER MySql OR MySqlConnectionString AND none of the Sql 
            return (((parameter.WellKnownTags & DeploymentWellKnownTag.MySql) != 0 ||
                     (parameter.WellKnownTags & DeploymentWellKnownTag.MySqlConnectionString) != 0) &&
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.Sql) == 0 &&
                     (parameter.WellKnownTags & DeploymentWellKnownTag.SqlConnectionString) == 0));
        }

        public bool IsSqlOnlyTag(DeploymentSyncParameter parameter)
        {
            // it should be EITHER Sql OR SqlConnectionString AND none of the Sql 
            return (((parameter.WellKnownTags & DeploymentWellKnownTag.Sql) != 0 ||
                     (parameter.WellKnownTags & DeploymentWellKnownTag.SqlConnectionString) != 0) &&
                    ((parameter.WellKnownTags & DeploymentWellKnownTag.MySql) == 0 &&
                     (parameter.WellKnownTags & DeploymentWellKnownTag.MySqlConnectionString) == 0));
        }

        internal bool ParameterHasDefaultValue(DeploymentSyncParameter parameter)
        {
            return !string.IsNullOrEmpty(parameter.DefaultValue);
        }

        public string GenerateParameterValue(DeploymentSyncParameterValidation parameterValidation)
        {
            string parameterValue = string.Empty;
            Random random = new Random(DateTime.Now.Millisecond);

            switch (parameterValidation.Kind)
            {
                case DeploymentSyncParameterValidationKind.AllowEmpty:
                case DeploymentSyncParameterValidationKind.None:
                    parameterValue = Helper.GetRandomString();
                    break;
                case DeploymentSyncParameterValidationKind.Boolean:
                    parameterValue = random.Next(1, 2) == 1 ? Boolean.FalseString : Boolean.TrueString;
                    break;
                case DeploymentSyncParameterValidationKind.Enumeration:
                    string[] enumValues = parameterValidation.ValidationString.Split(new char[]{','});
                    parameterValue = enumValues[random.Next(0, enumValues.Length - 1)];
                    break;
                case DeploymentSyncParameterValidationKind.RegularExpression:
                    parameterValue = StringGenerator.StringFromRegex(parameterValidation.ValidationString);
                    break;
            }
            return parameterValue;
        }

        #endregion
    }
}
