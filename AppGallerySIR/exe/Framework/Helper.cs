using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using Microsoft.Web.Deployment;

namespace AppGallery.SIR
{
    public class Helper
    {
        public static int RANDOM_STRING_MAX = 8;
        public const string LOWERCASE_CHARS = "abcdefghijklmnopqrstuvwxyz";
        public const string UPPERCASE_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const string NUMBERS = "1234567890";
        public const string SPECIAL_CHARS = "!@#$%^&*";

        private static int? _iisMajorVersion;
        private static int? _iisMinorVersion;
        private static string _assemblyVersion;

        public static int IISMajorVersion
        {
            get
            {
                if (!_iisMajorVersion.HasValue)
                {
                    RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\InetStp");
                    _iisMajorVersion = (int)regKey.GetValue("MajorVersion");                    
                }

                return _iisMajorVersion.Value;
            }
        }

        public static int IISMinorVersion
        {
            get
            {
                if (!_iisMinorVersion.HasValue)
                {
                    RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\InetStp");
                    _iisMinorVersion = (int)regKey.GetValue("MinorVersion");
                }

                return _iisMinorVersion.Value;
            }
        }

        public static string AssemblyVersion
        {
            get
            {
                if (string.IsNullOrEmpty(_assemblyVersion))
                {
                    _assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                }
                return _assemblyVersion;
            }
        }

        public static string FormattedTimeStampString
        {
            get
            {
                return "   [" + DateTime.Now.ToString() + "] ";
            }
        }

        public static bool IsAbsolutePhysicalPath(string path)
        {
            if (string.IsNullOrEmpty(path) || (path.Length < 3))
            {
                //too short
                return false;
            }
            else if ((path[1] == System.IO.Path.VolumeSeparatorChar) && IsDirectorySeparatorChar(path[2]))
            {
                //its like c:\
                return true;
            }
            else if (IsDirectorySeparatorChar(path[0]) && IsDirectorySeparatorChar(path[1]))
            {
                // its a share 
                return true;
            }
            else
            {
                //its unknown
                return false;
            }
        }

        private static bool IsDirectorySeparatorChar(char c)
        {
            if (c == System.IO.Path.DirectorySeparatorChar ||
                c == System.IO.Path.AltDirectorySeparatorChar)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string GetTimeStampString(DateTime dateTime)
        {
            return DateTime.Now.ToShortTimeString().Replace(':', '-').Replace(' ', '_');
        }
        
        public static bool IsUri(string packagePath)
        {
            if (packagePath.Contains("http") || packagePath.Contains("https"))
            {
                try
                {
                    new Uri(packagePath);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public static string GetRandomString()
        {
            StringBuilder meaningfulString = new StringBuilder();
            Random random = new Random(DateTime.Now.Millisecond);
            for (int i = 0; i < RANDOM_STRING_MAX; i++)
            {
                meaningfulString.Append(LOWERCASE_CHARS[random.Next(0, LOWERCASE_CHARS.Length)]);
            }
            return meaningfulString.ToString();
        }

        public static string GetRandomStrongPassword()
        {
            Random random = new Random(DateTime.Now.Millisecond);
            StringBuilder password = new StringBuilder();
            for (int i = 0; i < 2; i++)
            {
                password.Append(UPPERCASE_CHARS[random.Next(0, UPPERCASE_CHARS.Length)]);
                password.Append(NUMBERS[random.Next(0, NUMBERS.Length)]);
                password.Append(SPECIAL_CHARS[random.Next(0, SPECIAL_CHARS.Length)]);
                password.Append(LOWERCASE_CHARS[random.Next(0, LOWERCASE_CHARS.Length)]);
            }
            return password.ToString();
        }

        public static bool IsIISInstalled()
        {
            RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\InetStp");

            if (regKey == null)
            {                
                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool IsMsDeployInstalled()
        {
            RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\IIS Extensions\MSDeploy");

            if (regKey == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }               

        public static void TraceEventHandler(object sender, DeploymentTraceEventArgs e)
        {
            if (e.EventLevel == TraceLevel.Error)
            {
                Trace.TraceError(e.Message);
            }
            else if (e.EventLevel == TraceLevel.Warning)
            {
                Trace.TraceWarning(e.Message);
            }
            else
            {
                Trace.TraceInformation(e.Message);
            }
        }

        // TODO: This doesn't work, because the exe has the open handle on the temp file,
        // because shell won't release it
        public static void ForceFileDeletion(string filePath)
        {
            // cant think of anything better
            try
            {
                if ((File.GetAttributes(filePath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                }
                File.Delete(filePath);
            }
            catch (Exception) { /* swallow */ }
        }

        public static void DeleteFolder(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (string file in files)
                    {
                        ForceFileDeletion(file);
                    }

                    string[] directories = Directory.GetDirectories(folderPath);
                    foreach (string directory in directories)
                    {
                        DeleteFolder(directory);
                    }

                    Directory.Delete(folderPath);
                }
            }
            catch (Exception) { /* swallow */ }
        }
    }   
}
