//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Platform.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        private static bool             osChecked;
        private static string           osDescription;
        private static NetFramework?    netFramework = null;
        private static string           frameworkDescription;
        private static Version          frameworkVersion;
        private static bool             isWindows;
        private static WindowsEdition   windowsEdition;
        private static bool             isLinux;
        private static bool             isOSX;
        private static bool?            is64BitBuild;
        private static bool             isARM;
        private static bool?            isDevWorkstation;
        private static bool?            isKubernetes;

        /// <summary>
        /// Detects the current operating system.
        /// </summary>
        private static void DetectOS()
        {
            if (osChecked)
            {
                return;     // Already competed detection.
            }

            try
            {
                osDescription        = RuntimeInformation.OSDescription;
                frameworkDescription = RuntimeInformation.FrameworkDescription;
                isWindows            = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                isLinux              = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
                isOSX                = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
                isARM                = RuntimeInformation.OSArchitecture == Architecture.Arm ||
                                       RuntimeInformation.OSArchitecture == Architecture.Arm64;

                if (isWindows)
                {
                    // Examine registry to detect the Windows Edition.

                    var key       = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Is64BitOS ? RegistryView.Registry64 : RegistryView.Registry32);
                    var editionID = key.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion").GetValue("EditionID").ToString();

                    // $todo(jefflill): We're guessing at the server edition IDs here.

                    switch (editionID.ToLowerInvariant())
                    {
                        case "home":

                            windowsEdition = WindowsEdition.Home;
                            break;

                        case "professional":

                            windowsEdition = WindowsEdition.Professional;
                            break;

                        case "serverstandard":

                            windowsEdition = WindowsEdition.ServerStandard;
                            break;

                        case "serverenterprise":

                            windowsEdition = WindowsEdition.ServerEnterprise;
                            break;

                        case "serverdatacenter":

                            windowsEdition = WindowsEdition.ServerDatacenter;
                            break;

                        default:

                            windowsEdition = WindowsEdition.Unknown;
                            break;
                    }
                }
            }
            finally
            {
                // Set the global to true so we won't test again.

                osChecked = true;
            }
        }

        /// <summary>
        /// Returns the operation system description.
        /// </summary>
        public static string OSDescription
        {
            get
            {
                if (osChecked)
                {
                    return osDescription;
                }

                DetectOS();
                return osDescription;
            }
        }

        /// <summary>
        /// Returns <c>true</c> for 32-bit operating systems.
        /// </summary>
        public static bool Is32BitOS => !Environment.Is64BitOperatingSystem;

        /// <summary>
        /// Returns <c>true</c> for 64-bit operating systems.
        /// </summary>
        public static bool Is64BitOS => Environment.Is64BitOperatingSystem;

        /// <summary>
        /// Returns the .NET runtime description.
        /// </summary>
        public static string FrameworkDescription
        {
            get
            {
                if (osChecked)
                {
                    return frameworkDescription;
                }

                DetectOS();
                return frameworkDescription;
            }
        }

        /// <summary>
        /// Identifies the .NET runtime hosting the current process.
        /// </summary>
        public static NetFramework Framework
        {
            get
            {
                if (netFramework.HasValue)
                {
                    return netFramework.Value;
                }

                if (FrameworkDescription.StartsWith(".NET Core"))
                {
                    return (netFramework = NetFramework.Core).Value;
                }
                else if (FrameworkDescription.StartsWith(".NET Framework"))
                {
                    return (netFramework = NetFramework.NetFramework).Value;
                }
                else if (FrameworkDescription.StartsWith(".NET Native"))
                {
                    return (netFramework = NetFramework.Native).Value;
                }

                // .NET 5.0 and beyond will have framework descriptions like
                // ".NET 5.0.0", ".NET 6.0.0",...
                //
                // We're going to treat all of these as the new .NET 5+ framework
                // (the last framework you'll ever need :)

                var netRegex = new Regex(@"^.NET \d");

                if (netRegex.IsMatch(FrameworkDescription))
                {
                    return NetFramework.Net;
                }
                else
                {
                    return (netFramework = NetFramework.Unknown).Value;
                }
            }
        }

        /// <summary>
        /// Returns the current .NET runtime version hosting the current process.
        /// </summary>
        public static Version FrameworkVersion
        {
            get
            {
                if (frameworkVersion != null)
                {
                    return frameworkVersion;
                }

                string version;

                switch (Framework)
                {
                    case NetFramework.Core:

                        version = FrameworkDescription.Substring(".NET Core".Length).Trim();
                        break;

                    case NetFramework.NetFramework:

                        version = FrameworkDescription.Substring(".NET Framework".Length).Trim();
                        break;

                    case NetFramework.Net:

                        version = FrameworkDescription.Substring(".NET".Length).Trim();
                        break;

                    default:
                    case NetFramework.Native:

                        throw new NotImplementedException($"Framework runtime [{Framework}] not currently supported.");
                }

                return frameworkVersion = Version.Parse(version);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the application was built as 64-bit.
        /// </summary>
        public static bool Is64BitBuild
        {
            get
            {
                if (is64BitBuild.HasValue)
                {
                    return is64BitBuild.Value;
                }

                is64BitBuild = System.Runtime.InteropServices.Marshal.SizeOf<IntPtr>() == 8;

                return is64BitBuild.Value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the client was built as 32-bit.
        /// </summary>
        public static bool Is32BitBuild
        {
            get { return !Is64BitBuild; }
        }

        /// <summary>
        /// Indicates whether the current application is running on a developer workstation.
        /// This is determined by the presence of the <b>DEV_WORKSTATION</b> environment variable.
        /// </summary>
        public static bool IsDevWorkstation
        {
            get
            {
                if (isDevWorkstation.HasValue)
                {
                    return isDevWorkstation.Value;
                }

                isDevWorkstation = Environment.GetEnvironmentVariable("DEV_WORKSTATION") != null;

                return isDevWorkstation.Value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current process is running on a Windows variant
        /// operating system.
        /// </summary>
        public static bool IsWindows
        {
            get
            {
                if (osChecked)
                {
                    return isWindows;
                }

                DetectOS();
                return isWindows;
            }
        }

        /// <summary>
        /// Ensures that the Windows is the current operating system.
        /// </summary>
        private static void EnsureWindows()
        {
            if (!isWindows)
            {
                throw new NotSupportedException("This property works only on Windows.");
            }
        }

        /// <summary>
        /// Identifies the current Windows edition (home, pro, server,...).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when not running on Windows.</exception>
        public static WindowsEdition WindowsEdition
        {
            get
            {
                EnsureWindows();

                if (!osChecked)
                {
                    DetectOS();
                }

                return windowsEdition;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current process is running on a Linux variant
        /// operating system.
        /// </summary>
        public static bool IsLinux
        {
            get
            {
                if (osChecked)
                {
                    return isLinux;
                }

                DetectOS();
                return isLinux;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current process is running on Mac OSX.
        /// </summary>
        public static bool IsOSX
        {
            get
            {
                if (osChecked)
                {
                    return isOSX;
                }

                DetectOS();
                return isOSX;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current process is runniong on an ARM processor.
        /// </summary>
        public static bool IsARM
        {
            get
            {
                if (osChecked)
                {
                    return isOSX;
                }

                DetectOS();
                return isARM;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current process is running within a CI environment
        /// such as GitHub Actions.  This checks for this environment variable: <b>CI=true</b>.
        /// </summary>
        public static bool IsCI
        {
            get
            {
                var value = Environment.GetEnvironmentVariable("CI");

                if (value == null)
                {
                    return false;
                }

                return value.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current process is running as a container on Kubernetes.
        /// </summary>
        public static bool IsKubernetes
        {
            get
            {
                // We'll use the existence of the KUBERNETES_SERVICE_HOST environment 
                // variable to detect this:
                //
                //      https://kubernetes.io/docs/concepts/services-networking/connect-applications-service/#environment-variables

                if (isKubernetes.HasValue)
                {
                    return isKubernetes.Value;
                }

                if (Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null)
                {
                    isKubernetes = true;
                }
                else
                {
                    isKubernetes = false;
                }

                return isKubernetes.Value;
            }
        }

        /// <summary>
        /// Returns a dictionary mapping optional Windows feature names to a <see cref="WindowsFeatureStatus"/>
        /// indicating feature installation status.
        /// </summary>
        /// <returns>The feature dictionary.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not running on Windows.</exception>
        /// <remarks>
        /// <note>
        /// The feature names are in English and the lookup is case-insensitive.
        /// </note>
        /// </remarks>
        public static Dictionary<string, WindowsFeatureStatus> GetWindowsOptionalFeatures()
        {
            EnsureWindows();

            // We're going to use the DSIM.EXE app to list these.  The table output
            // will look like:
            //
            //      Deployment Image Servicing and Management tool
            //      Version: 10.0.19041.844
            //
            //      Image Version: 10.0.19042.1083
            //
            //      Features listing for package : Microsoft-Windows-Foundation-Package~31bf3856ad364e35~amd64~~10.0.19041.1
            //
            //
            //      ------------------------------------------- | --------
            //      Feature Name                                | State
            //      ------------------------------------------- | --------
            //      Printing - PrintToPDFServices-Features      | Enabled
            //      Printing - XPSServices-Features             | Enabled
            //      TelnetClient                                | Disabled
            //      TFTP                                        | Disabled
            //      LegacyComponents                            | Disabled
            //      DirectPlay                                  | Disabled
            //      Printing-Foundation-Features                | Enabled
            //      Printing-Foundation-InternetPrinting-Client | Enabled
            //
            //      ...
            //
            // We're simply going to parse the lines with a pipe ("|").

            var response = NeonHelper.ExecuteCapture("dism.exe",
                new object[]
                {
                    "/Online",
                    "/English",
                    "/Get-Features",
                    "/Format:table"
                });

            response.EnsureSuccess();

            var featureMap = new Dictionary<string, WindowsFeatureStatus>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var line in response.OutputText.ToLines())
            {
                if (!line.StartsWith("----") && line.Contains('|'))
                {
                    var fields = line.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    var status = WindowsFeatureStatus.Unknown;

                    fields[0] = fields[0].Trim();
                    fields[1] = fields[1].Trim();

                    if (fields[0] == "Feature Name")
                    {
                        continue;   // Ignore column headers
                    }

                    switch (fields[1].ToLowerInvariant())
                    {
                        case "disabled":

                            status = WindowsFeatureStatus.Disabled;
                            break;

                        case "enabled":

                            status = WindowsFeatureStatus.Enabled;
                            break;

                        case "enable pending":

                            status = WindowsFeatureStatus.EnabledPending;
                            break;
                    }

                    featureMap[fields[0]] = status;
                }
            }

            return featureMap;
        }

        /// <summary>
        /// Returns the installation status for the named feature.
        /// </summary>
        /// <param name="feature">Specifies the <b>English</b> name for the feature.</param>
        /// <returns>The <see cref="WindowsFeatureStatus"/> for the feature.</returns>
        /// <remarks>
        /// <para>
        /// You'll need to pass the feature name in English.  You can list possible feature
        /// names by executing this in your command shell:
        /// </para>
        /// <example>
        /// dism /Online /English /Get-Features /Format:table
        /// </example>
        /// <note>
        /// <see cref="WindowsFeatureStatus.Unknown"/> will be returned for unknown features.
        /// </note>
        /// </remarks>
        public static WindowsFeatureStatus GetWindowsOptionalFeatureStatus(string feature)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(feature), nameof(feature));

            if (GetWindowsOptionalFeatures().TryGetValue(feature, out var status))
            {
                return status;
            }
            else
            {
                return WindowsFeatureStatus.Unknown;
            }
        }

        /// <summary>
        /// Enables an optional Windows feature, returning an indication of whether a 
        /// Windows restart is required to complete the installation.
        /// </summary>
        /// <returns><c>true</c> if a restart is required.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the feature does't exist.</exception>
        /// <remarks>
        /// This method does nothing when the feature is already enabled
        /// or has been enabled but is waiting for a restart.
        /// </remarks>
        public static bool EnableOptionalWindowsFeature(string feature)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(feature), nameof(feature));

            switch (GetWindowsOptionalFeatureStatus(feature))
            {
                case WindowsFeatureStatus.Unknown:

                    throw new InvalidOperationException($"Unknown Windows Feature: {feature}");

                case WindowsFeatureStatus.Enabled:

                    var response = NeonHelper.ExecuteCapture("dism.exe",
                        new object[]
                        {
                            "/Online",
                            "/English",
                            "/Enable-Feature",
                            $"/FeatureName:{feature}"
                        });

                    response.EnsureSuccess();

                    return GetWindowsOptionalFeatureStatus(feature) == WindowsFeatureStatus.EnabledPending;

                case WindowsFeatureStatus.EnabledPending:

                    return true;

                case WindowsFeatureStatus.Disabled:

                    return false;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Disables an optional Windows feature.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the feature does't exist or is enabled and waiting for a Windows restart.
        /// </exception>
        /// <remarks>
        /// This method does nothing when the feature is already disabled.
        /// </remarks>
        public static void DisableOptionalWindowsFeature(string feature)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(feature), nameof(feature));

            switch (GetWindowsOptionalFeatureStatus(feature))
            {
                case WindowsFeatureStatus.Unknown:

                    throw new InvalidOperationException($"Unknown Windows Feature: {feature}");

                case WindowsFeatureStatus.Enabled:

                    var response = NeonHelper.ExecuteCapture("dism.exe",
                        new object[]
                        {
                            "/Online",
                            "/English",
                            "/Disable-Feature",
                            $"/FeatureName:{feature}"
                        });

                    response.EnsureSuccess();
                    break;

                case WindowsFeatureStatus.EnabledPending:

                    throw new InvalidOperationException($"Windows Feature install is pending: {feature}");

                case WindowsFeatureStatus.Disabled:

                    return;

                default:

                    throw new NotImplementedException();
            }
        }
    }
}
