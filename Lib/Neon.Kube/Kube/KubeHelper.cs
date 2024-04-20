//-----------------------------------------------------------------------------
// FILE:        KubeHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using DiscUtils.Iso9660;

using IdentityModel.OidcClient;

using k8s;
using k8s.KubeConfigModels;
using k8s.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Deployment;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.K8s;
using Neon.Kube.BuildInfo;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Glauth;
using Neon.Net;
using Neon.Retry;
using Neon.Tasks;

using SharpCompress.Readers;

namespace Neon.Kube
{
    /// <summary>
    /// cluster related utilties.
    /// </summary>
    public static class KubeHelper
    {
        private static Guid                 clientId;
        private static KubeConfig           cachedConfig;
        private static string               cachedNeonKubeUserFolder;
        private static string               cachedRunFolder;
        private static string               cachedLogFolder;
        private static string               cachedLogDetailsFolder;
        private static string               cachedTempFolder;
        private static string               cachedSetupFolder;
        private static string               cachedPasswordsFolder;
        private static string               cachedCacheFolder;
        private static string               cachedDesktopCommonFolder;
        private static string               cachedDesktopFolder;
        private static string               cachedDesktopLogFolder;
        private static string               cachedDesktopHypervFolder;
        private static KubeClientConfig     cachedClientConfig;
        private static string               cachedInstallFolder;
        private static string               cachedToolsFolder;
        private static string               cachedDevopmentFolder;
        private static string               cachedNodeContainerImagesFolder;
        private static IStaticDirectory     cachedResources;
        private static string               cachedVmImageFolder;
        private static string               cachedUserSshFolder;
        private static string               cachedNeonCliPath;
        private static object               jsonConverterLock = new object();

        private static List<KeyValuePair<string, object>> cachedTelemetryTags;

        /// <summary>
        /// CURL command common options.
        /// </summary>
        public const string CurlOptions = "-4fsSL --retry 10 --retry-delay 30 --max-redirs 10";

        /// <summary>
        /// Clears all cached items.
        /// </summary>
        private static void ClearCachedItems()
        {
            cachedConfig                    = null;
            cachedNeonKubeUserFolder        = null;
            cachedRunFolder                 = null;
            cachedLogFolder                 = null;
            cachedLogDetailsFolder          = null;
            cachedTempFolder                = null;
            cachedSetupFolder               = null;
            cachedPasswordsFolder           = null;
            cachedCacheFolder               = null;
            cachedDesktopCommonFolder       = null;
            cachedDesktopFolder             = null;
            cachedDesktopHypervFolder       = null;
            cachedClientConfig              = null;
            cachedInstallFolder             = null;
            cachedToolsFolder               = null;
            cachedDevopmentFolder           = null;
            cachedNodeContainerImagesFolder = null;
            cachedResources                 = null;
            cachedVmImageFolder             = null;
            cachedUserSshFolder             = null;
            cachedNeonCliPath               = null;
        }

        /// <summary>
        /// <para>
        /// Returns a unique ID for the client installation.  This used for identifying the
        /// client for logs and traces so we can correlate problems specific users are seeing.
        /// </para>
        /// <note>
        /// This is persisted to: <b>~/.neonkube/desktop/client-id</b>
        /// </note>
        /// </summary>
        public static Guid ClientId
        {
            get
            {
                if (clientId != Guid.Empty)
                {
                    return clientId;
                }

                // We'll use this GUID for the session if we're unable to read
                // the [client-id] file.

                clientId = Guid.NewGuid();

                try
                {
                    var clientIdPath = Path.Combine(KubeHelper.DesktopFolder, "client-id");

                    if (File.Exists(clientIdPath))
                    {
                        clientId = Guid.ParseExact(File.ReadAllLines(clientIdPath).First(), "d");
                    }
                    else
                    {
                        File.WriteAllText(clientIdPath, clientId.ToString("d"));
                    }
                }
                catch (IOException)
                {
                    // Ignoring this.
                }
                catch (FormatException)
                {
                    // Ignoring this too.
                }

                return clientId;
            }
        }

        /// <summary>
        /// Returns the <see cref="IStaticDirectory"/> for the assembly's resources.
        /// </summary>
        public static IStaticDirectory Resources
        {
            get
            {
                if (cachedResources != null)
                {
                    return cachedResources;
                }

                return cachedResources = Assembly.GetExecutingAssembly().GetResourceFileSystem("Neon.Kube.Resources");
            }
        }

        /// <summary>
        /// <para>
        /// Determines whether a name is a valid Kubernetes name.
        /// </para>
        /// <list type="bullet">
        /// <item>contain no more than 253 characters</item>
        /// <item>contain only lowercase alphanumeric characters, '-' or '.'</item>
        /// <item>start with an alphanumeric character</item>
        /// <item>end with an alphanumeric character</item>
        /// </list>
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <exception cref="ArgumentNullException">Thrown for null or empty names.</exception>
        /// <exception cref="FormatException">Thrown for invalid names.</exception>
        public static void CheckName(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (name.Length > 253)
            {
                throw new FormatException($"Name exceeds 253 characters: {name}");
            }

            if (!char.IsLetterOrDigit(name.First()))
            {
                throw new FormatException($"Name starts with a non-alphanum character: {name}");
            }

            if (!char.IsLetterOrDigit(name.Last()))
            {
                throw new FormatException($"Name ends with a non-alphanum character: {name}");
            }

            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-')
                {
                    continue;
                }

                throw new FormatException($"Name includes invalid character: [{ch}]");
            }
        }

        /// <summary>
        /// Reads a file as text, retrying if the file is already open.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The file text.</returns>
        /// <remarks>
        /// It's possible for the configuration file to be temporarily opened
        /// by another process (e.g. the NEONDESKTOP application or a 
        /// command line tool).  Rather than throw an exception, we're going
        /// to retry the operation a few times.
        /// </remarks>
        internal static string ParseTextFileWithRetry(string path)
        {
            var retry = new LinearRetryPolicy(typeof(IOException), maxAttempts: 10, retryInterval: TimeSpan.FromMilliseconds(200));
            var text  = string.Empty;

            retry.Invoke(
                () =>
                {
                    text = File.ReadAllText(path);
                });

            return text;
        }

        /// <summary>
        /// Writes a file as text, retrying if the file is already open.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="text">The text to be written.</param>
        /// <remarks>
        /// It's possible for the configuration file to be temporarily opened
        /// by another process (e.g. the NEONDESKTOP application or a 
        /// command line tool).  Rather than throw an exception, we're going
        /// to retry the operation a few times.
        /// </remarks>
        internal static string WriteFileTextWithRetry(string path, string text)
        {
            var retry = new LinearRetryPolicy(typeof(IOException), maxAttempts: 10, retryInterval: TimeSpan.FromMilliseconds(200));

            retry.Invoke(
                () =>
                {
                    File.WriteAllText(path, text);
                });

            return text;
        }

        /// <summary>
        /// Returns the path to the current user's <b>.neonkube</b> folder.
        /// </summary>
        public static string StandardNeonKubeFolder
        {
            get
            {
                var neonKubeHomeFolder = Path.Combine(NeonHelper.UserHomeFolder, ".neonkube");

                Directory.CreateDirectory(neonKubeHomeFolder);
                
                return neonKubeHomeFolder;
            }
        }

        /// <summary>
        /// Accesses the NEONDESKTOP client configuration.
        /// </summary>
        public static KubeClientConfig ClientConfig
        {
            get
            {
                if (cachedClientConfig != null)
                {
                    return cachedClientConfig;
                }

                var clientStatePath = Path.Combine(KubeHelper.DesktopFolder, "config.json");

                try
                {
                    cachedClientConfig = NeonHelper.JsonDeserialize<KubeClientConfig>(ParseTextFileWithRetry(clientStatePath));

                    ClientConfig.Validate();
                }
                catch
                {
                    // The file doesn't exist yet or could not be parsed, so we'll
                    // generate a new file with default settings.

                    cachedClientConfig = new KubeClientConfig();

                    SaveClientState();
                }

                return cachedClientConfig;
            }

            set
            {
                Covenant.Requires<ArgumentNullException>(value != null, nameof(ClientConfig));

                value.Validate();
                cachedClientConfig = value;
                SaveClientState();
            }
        }

        /// <summary>
        /// Loads or reloads the <see cref="ClientConfig"/>.
        /// </summary>
        /// <returns>The client configuration.</returns>
        public static KubeClientConfig LoadClientConfig()
        {
            cachedClientConfig = null;

            return ClientConfig;
        }

        /// <summary>
        /// Persists the <see cref="ClientConfig"/> to disk.
        /// </summary>
        public static void SaveClientState()
        {
            var clientStatePath = Path.Combine(KubeHelper.DesktopFolder, "config.json");

            ClientConfig.Validate();
            WriteFileTextWithRetry(clientStatePath, NeonHelper.JsonSerialize(cachedClientConfig, Formatting.Indented));
        }

        /// <summary>
        /// Returns the <see cref="KubeClientPlatform"/> for the current workstation.
        /// </summary>
        public static KubeClientPlatform HostPlatform
        {
            get
            {
                if (NeonHelper.IsLinux)
                {
                    return KubeClientPlatform.Linux;
                }
                else if (NeonHelper.IsOSX)
                {
                    return KubeClientPlatform.Osx;
                }
                else if (NeonHelper.IsWindows)
                {
                    return KubeClientPlatform.Windows;
                }
                else
                {
                    throw new NotSupportedException("The current workstation opersating system is not supported.");
                }
            }
        }

        /// <summary>
        /// Determines whether a cluster hosting environment deploys to the cloud.
        /// </summary>
        /// <param name="hostingEnvironment">Specifies the hosting environment.</param>
        /// <returns><c>true</c> for cloud environments.</returns>
        public static bool IsCloudEnvironment(HostingEnvironment hostingEnvironment)
        {
            switch (hostingEnvironment)
            {
                // On-premise environments

                case HostingEnvironment.BareMetal:
                case HostingEnvironment.HyperV:
                case HostingEnvironment.XenServer:

                    return false;

                // Cloud environments

                case HostingEnvironment.Aws:
                case HostingEnvironment.Azure:
                case HostingEnvironment.Google:

                    return true;

                default:

                    throw new NotImplementedException("Unexpected hosting environment.");
            }
        }

        /// <summary>
        /// Determines whether NEONFORGE collects revenue from a cluster hosting environment.
        /// </summary>
        /// <param name="hostingEnvironment">Specifies the hosting environment.</param>
        /// <returns><c>true</c> for paid environments.</returns>
        public static bool IsPaidHostingEnvironment(HostingEnvironment hostingEnvironment) => IsCloudEnvironment(hostingEnvironment);

        /// <summary>
        /// Determines whether a cluster hosting environment is available only for NEONFORGE
        /// premium (closed-source) related projects.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for enteprise/closed-source related projects.</returns>
        public static bool IsPremiumEnvironment(HostingEnvironment hostingEnvironment)
        {
            switch (hostingEnvironment)
            {
                default:

                    return false;
            }
        }

        /// <summary>
        /// Determines whether a cluster hosting environment deploys on-premise.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for on-premise environments.</returns>
        public static bool IsOnPremiseEnvironment(HostingEnvironment hostingEnvironment)
        {
            return !IsCloudEnvironment(hostingEnvironment);
        }

        /// <summary>
        /// Determines whether a cluster hosting environment deploys to on-premise hypervisors.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for on-premise environments.</returns>
        public static bool IsOnPremiseHypervisorEnvironment(HostingEnvironment hostingEnvironment)
        {
            return hostingEnvironment == HostingEnvironment.HyperV ||
                   hostingEnvironment == HostingEnvironment.XenServer;
        }

        /// <summary>k
        /// <para>
        /// Returns the path to the Windows Desktop Service Unix domain socket.
        /// </para>
        /// <note>
        /// The Neon Windows Desktop Service runs in the background for all users so
        /// the socket will be located within the Windows program data folder.
        /// </note>
        /// </summary>
        public static string WinDesktopServiceSocketPath => Path.Combine(DesktopCommonFolder, "service.sock");

        /// <summary>
        /// Returns the path to the <b>.ssh</b> folder within user's home folder.
        /// </summary>
        public static string UserSshFolder
        {
            get
            {
                if (cachedUserSshFolder != null)
                {
                    return cachedUserSshFolder;
                }

                cachedUserSshFolder = Path.Combine(NeonHelper.UserHomeFolder, ".ssh");

                Directory.CreateDirectory(cachedUserSshFolder);

                return cachedUserSshFolder;
            }
        }

        /// <summary>
        /// Returns the path the folder holding the user specific cluster logins and other files.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string NeonKubeUserFolder
        {
            get
            {
                if (cachedNeonKubeUserFolder != null)
                {
                    return cachedNeonKubeUserFolder;
                }

                cachedNeonKubeUserFolder = Path.Combine(NeonHelper.UserHomeFolder, ".neonkube");

                Directory.CreateDirectory(cachedNeonKubeUserFolder);

                return cachedNeonKubeUserFolder;
            }
        }

        /// <summary>
        /// Returns the directory path where the [neon run CMD ...] will copy secrets and run the command.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string RunFolder
        {
            get
            {
                if (cachedRunFolder != null)
                {
                    return cachedRunFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "run");

                Directory.CreateDirectory(path);

                return cachedRunFolder = path;
            }
        }

        /// <summary>
        /// Returns the default directory path where neon-cli logs will be written.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string LogFolder
        {
            get
            {
                if (cachedLogFolder != null)
                {
                    return cachedLogFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "log");

                Directory.CreateDirectory(path);

                return cachedLogFolder = path;
            }
        }

        /// <summary>
        /// Returns the default directory path where neon-cli cluster details will be written.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string LogDetailsFolder
        {
            get
            {
                if (cachedLogDetailsFolder != null)
                {
                    return cachedLogDetailsFolder;
                }

                var path = Path.Combine(LogFolder, "details");

                Directory.CreateDirectory(path);

                return cachedLogDetailsFolder = path;
            }
        }

        /// <summary>
        /// Returns the path the user specific NEONKUBE temporary folder, creating the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage clusters.
        /// </remarks>
        public static string TempFolder
        {
            get
            {
                if (cachedTempFolder != null)
                {
                    return cachedTempFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "temp");

                Directory.CreateDirectory(path);

                return cachedTempFolder = path;
            }
        }

        /// <summary>
        /// Returns the path to the Kubernetes configuration file.
        /// </summary>
        public static string KubeConfigPath
        {
            get
            {
                var kubeConfigVar  = Environment.GetEnvironmentVariable("KUBECONFIG");
                var kubeConfigPath = (string)null;

                if (string.IsNullOrEmpty(kubeConfigVar))
                {
                    kubeConfigPath = Path.Combine(NeonHelper.UserHomeFolder, ".kube", "config");
                }
                else
                {
                    kubeConfigPath = kubeConfigVar.Split(';').Where(variable => variable.Contains("config")).FirstOrDefault();
                }

                Directory.CreateDirectory(Path.GetDirectoryName(kubeConfigPath));

                return kubeConfigPath;
            }
        }

        /// <summary>
        /// Returns the path the folder containing the temporary setup state files, creating the folder 
        /// if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// This folder holds <see cref="KubeSetupState"/> for clusters in the process of being prepared and setup. 
        /// Files will be  named like <b><i>CLUSTER-NAME</i>.json</b>
        /// </remarks>
        public static string SetupFolder
        {
            get
            {
                if (cachedSetupFolder != null)
                {
                    return cachedSetupFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "setup");

                Directory.CreateDirectory(path);

                return cachedSetupFolder = path;
            }
        }

        /// <summary>
        /// Returns the path the folder used for holding Kubernetes related tools when running
        /// <b>neon-cli</b> as a developer, creating the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string ToolsFolder
        {
            get
            {
                if (cachedToolsFolder != null)
                {
                    return cachedToolsFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "tools");

                Directory.CreateDirectory(path);

                return cachedToolsFolder = path;
            }
        }

        /// <summary>
        /// Returns the path the folder used by NEONKUBE development tools, 
        /// creating the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string DevelopmentFolder
        {
            get
            {
                if (cachedDevopmentFolder != null)
                {
                    return cachedDevopmentFolder;
                }

                var path = Path.Combine(NeonHelper.UserHomeFolder, ".neonkube-dev");

                Directory.CreateDirectory(path);

                return cachedDevopmentFolder = path;
            }
        }

        /// <summary>
        /// <para>
        /// Returns the path the folder used by NEONKUBE development tools to
        /// cache the packed container image files used to prepare NEONKUBE node
        /// images, creating the folder if it doesn't already exist.
        /// </para>
        /// <note>
        /// The path returned will always be within the user's original HOME
        /// folder.  This does not honor custom HOME folders set via 
        /// <see cref="NeonHelper.SetUserHomeFolder(string)"/>.  We do this
        /// because we use this folder to cache these images so we don't 
        /// need to rebuild them multiple times for a release.
        /// </note>
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string NodeContainerImagesFolder
        {
            get
            {
                if (cachedNodeContainerImagesFolder != null)
                {
                    return cachedNodeContainerImagesFolder;
                }

                var path = Path.Combine(NeonHelper.DefaultUserHomeFolder, ".neonkube-dev", "node-container-images");

                Directory.CreateDirectory(path);

                return cachedNodeContainerImagesFolder = path;
            }
        }

        /// <summary>
        /// Returns path to the folder holding the encryption passwords.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string PasswordsFolder
        {
            get
            {
                if (cachedPasswordsFolder != null)
                {
                    return cachedPasswordsFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "passwords");

                Directory.CreateDirectory(path);

                return cachedPasswordsFolder = path;
            }
        }

        /// <summary>
        /// Returns the path to the NEONDESKTOP state folder.
        /// </summary>
        public static string DesktopFolder
        {
            get
            {
                if (cachedDesktopFolder != null)
                {
                    return cachedDesktopFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "desktop");

                Directory.CreateDirectory(path);

                return cachedDesktopFolder = path;
            }
        }

        /// <summary>
        /// Returns path to the NEONDESKTOP log folder.
        /// </summary>
        public static string DesktopLogFolder
        {
            get
            {
                if (cachedDesktopLogFolder != null)
                {
                    return cachedDesktopLogFolder;
                }

                var path = Path.Combine(DesktopFolder, "log");

                Directory.CreateDirectory(path);

                return cachedDesktopLogFolder = path;
            }
        }

        /// <summary>
        /// Returns path to the NEONDESKTOP Hyper-V state folder.
        /// </summary>
        public static string DesktopHypervFolder
        {
            get
            {
                if (cachedDesktopHypervFolder != null)
                {
                    return cachedDesktopHypervFolder;
                }

                var path = Path.Combine(DesktopFolder, "hyperv");

                Directory.CreateDirectory(path);

                return cachedDesktopHypervFolder = path;
            }
        }

        /// <summary>
        /// Returns the path the folder containing cached files for various environments.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string CacheFolder
        {
            get
            {
                if (cachedCacheFolder != null)
                {
                    return cachedCacheFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "cache");

                Directory.CreateDirectory(path);

                return cachedCacheFolder = path;
            }
        }

        /// <summary>
        /// Returns the path to the folder containing cached files for the specified platform.
        /// </summary>
        /// <param name="platform">Identifies the platform.</param>
        /// <returns>The folder path.</returns>
        public static string GetPlatformCacheFolder(KubeClientPlatform platform)
        {
            string subfolder;

            switch (platform)
            {
                case KubeClientPlatform.Linux:

                    subfolder = "linux";
                    break;

                case KubeClientPlatform.Osx:

                    subfolder = "osx";
                    break;

                case KubeClientPlatform.Windows:

                    subfolder = "windows";
                    break;

                default:

                    throw new NotImplementedException($"Platform [{platform}] is not implemented.");
            }

            var path = Path.Combine(CacheFolder, subfolder);

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// <para>
        /// Returns the path to the global NEONDESKTOP program data folder.  This is used for information
        /// to be shared across all users as well as between the user programs and the neon-desktop-service.
        /// </para>
        /// <note>
        /// All users will have read/write access to files in this folder.
        /// </note>
        /// </summary>
        public static string DesktopCommonFolder
        {
            get
            {
                if (cachedDesktopCommonFolder != null)
                {
                    return cachedDesktopCommonFolder;
                }

                cachedDesktopCommonFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NEONFORGE", "neon-desktop");

                if (OperatingSystem.IsWindowsVersionAtLeast(10))
                {
                    if (!Directory.Exists(cachedDesktopCommonFolder))
                    {
                        Directory.CreateDirectory(cachedDesktopCommonFolder);

                        // Grant all users access to this folder.  The simple approach would be to allow "Users"
                        // but apparently that only works for English Windows installations.  We'll need to look up
                        // the everyone account and use its actual name.
                        //
                        // We also need to remove any inherited ACLs first so this is a bit more complex than you'd
                        // think.  This includes some hints about how this works:
                        //
                        //      https://stackoverflow.com/questions/51277338/remove-users-group-permission-for-folder-inside-programdata

                        var builtUnsersSid    = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                        var directoryInfo     = new DirectoryInfo(cachedDesktopCommonFolder);
                        var directorySecurity = directoryInfo.GetAccessControl();

                        // Disable inherited ACLs.

                        directorySecurity.SetAccessRuleProtection(false, false);
                        directoryInfo.SetAccessControl(directorySecurity);

                        // Fetch the updated ACLs and add the new ACL.

                        directorySecurity = directoryInfo.GetAccessControl();

                        directorySecurity.AddAccessRule(new FileSystemAccessRule(builtUnsersSid, FileSystemRights.FullControl, AccessControlType.Allow));
                        directoryInfo.SetAccessControl(directorySecurity);
                    }
                }

                return cachedDesktopCommonFolder;
            }
        }

        /// <summary>
        /// Returns the path to the cached file for a specific named component with optional version.
        /// </summary>
        /// <param name="platform">Identifies the platform.</param>
        /// <param name="component">The component name.</param>
        /// <param name="version">The component version (or <c>null</c>).</param>
        /// <returns>The component file path.</returns>
        public static string GetCachedComponentPath(KubeClientPlatform platform, string component, string version)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(component), nameof(component));

            string path;

            if (string.IsNullOrEmpty(version))
            {
                path = Path.Combine(GetPlatformCacheFolder(platform), component);
            }
            else
            {
                path = Path.Combine(GetPlatformCacheFolder(platform), $"{component}-{version}");
            }

            return path;
        }

        /// <summary>
        /// The name of the user's cluster virtual machine image cache folder.
        /// </summary>
        public const string VmImageFolderName = "images";

        /// <summary>
        /// Returns the path to the current user's cluster virtual machine 
        /// image cache folder, creating the directory if it doesn't already exist.
        /// </summary>
        /// <returns>The path to the cluster setup folder.</returns>
        /// <remarks>
        /// In very special situations, you may use this to set a custom cache folder.
        /// </remarks>
        public static string VmImageFolder
        {
            get
            {
                if (cachedVmImageFolder != null)
                {
                    return cachedVmImageFolder;
                }

                var path = Path.Combine(StandardNeonKubeFolder, VmImageFolderName);

                Directory.CreateDirectory(path);

                return cachedVmImageFolder = path;
            }

            set
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(value), nameof(VmImageFolder));

                Directory.CreateDirectory(value);

                cachedCacheFolder = value;
            }
        }

        /// <summary>
        /// Returns the path to the neon installation folder.  This is where the either
        /// <b>neon-cli</b> or <b>neon-desktop</b> are installed.  This is used to determine
        /// where tools like <b>pwsh</b> and <b>ssh-keygen</b> are located.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One of <b>neon-cli</b> or <b>neon-desktop</b> are allowed to be installed on
        /// a user's workstation and the <b>NEON_INSTALL_FOLDER</b> environment variable
        /// will be set during installation to point to the program installation folder.
        /// </para>
        /// <para>
        /// This folder will be structured like for a <b>neon-cli only</b>installation:
        /// </para>
        /// <code>
        /// C:\Program Files\NEONFORGE\neon-cli\
        ///     .                       # neon-cli binaries
        ///     neon-desktop-service    # neon-desktop-service binaries
        ///     ssh\                    # SSH related tools
        /// </code>
        /// <para>
        /// and this for <b>neon-desktop</b> (which includes <b>neon-cli</b>):
        /// </para>
        /// <code>
        /// C:\Program Files\NEONFORGE\neon-desktop\
        ///     .                       # neon-desktop and neon-cli binaries
        ///     neon-desktop-service    # neon-desktop-service binaries
        ///     ssh\                    # SSH related tools
        /// </code>
        /// </remarks>
        public static string InstallFolder
        {
            get
            {
                if (cachedInstallFolder != null)
                {
                    return cachedInstallFolder;
                }

                return cachedInstallFolder = Environment.GetEnvironmentVariable("NEON_INSTALL_FOLDER");
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current assembly was built from the production <b>PROD</b> 
        /// source code branch.
        /// </summary>
#pragma warning disable 0436
        public static bool IsRelease => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase);
#pragma warning restore 0436

        /// <summary>
        /// Loads or reloads the Kubernetes configuration.
        /// </summary>
        /// <returns>The <see cref="KubeConfig"/>.</returns>
        public static KubeConfig LoadConfig()
        {
            return cachedConfig = KubeConfig.Load();
        }

        /// <summary>
        /// Returns the user's current <see cref="Config.KubeConfig"/>.
        /// </summary>
        public static KubeConfig KubeConfig
        {
            get
            {
                if (cachedConfig != null)
                {
                    return cachedConfig;
                }

                return LoadConfig();
            }
        }

        /// <summary>
        /// Rewrites the local kubeconfig file.
        /// </summary>
        /// <param name="config">The new configuration.</param>
        public static void SetConfig(KubeConfig config)
        {
            Covenant.Requires<ArgumentNullException>(config != null, nameof(config));

            cachedConfig = config;

            config.Save(KubeHelper.KubeConfigPath);
        }

        /// <summary>
        /// Used to initialize <see cref="KubernetesJson"/>.
        /// </summary>
        public static void InitializeJson()
        {
            lock (jsonConverterLock)
            {
                var kubernetesJsonType = typeof(KubernetesJson).Assembly.GetType("k8s.KubernetesJson");
                
                RuntimeHelpers.RunClassConstructor(kubernetesJsonType.TypeHandle);

                var member  = kubernetesJsonType.GetField("JsonSerializerOptions", BindingFlags.Static | BindingFlags.NonPublic);
                var options = (JsonSerializerOptions)member.GetValue(kubernetesJsonType);

                var converters = options.Converters.Where(c => c.GetType() == typeof(JsonStringEnumMemberConverter));
                if (!converters.Any())
                {
                    options.Converters.Insert(0, new JsonStringEnumMemberConverter());
                }
            }
        }

        /// <summary>
        /// Sets the current Kubernetes config context.
        /// </summary>
        /// <param name="contextName">The context name or <c>null</c> to clear the current context.</param>
        /// <exception cref="ArgumentException">Thrown if the context specified doesn't exist.</exception>
        public static void SetCurrentContext(KubeContextName contextName)
        {
            if (contextName == null)
            {
                KubeConfig.CurrentContext = null;
            }
            else
            {
                var newContext = KubeConfig.GetContext(contextName);

                if (newContext == null)
                {
                    throw new ArgumentException($"Kubernetes [context={contextName}] does not exist.", nameof(contextName));
                }

                KubeConfig.CurrentContext = (string)contextName;
            }

            KubeConfig.Save();
        }

        /// <summary>
        /// Sets the current Kubernetes config context by string name.
        /// </summary>
        /// <param name="contextName">The context name or <c>null</c> to clear the current context.</param>
        /// <exception cref="ArgumentException">Thrown if the context specified doesn't exist.</exception>
        public static void SetCurrentContext(string contextName)
        {
            SetCurrentContext((KubeContextName)contextName);
        }

        /// <summary>
        /// Returns the current <see cref="KubeConfigContext"/> from the KubeContext or
        /// <c>null</c> when no context is selected.
        /// </summary>
        public static KubeConfigContext CurrentContext
        {
            get
            {
                if (KubeConfig == null || string.IsNullOrEmpty(KubeConfig.CurrentContext))
                {
                    return null;
                }
                else
                {
                    return KubeConfig.GetContext(KubeConfig.CurrentContext);
                }
            }
        }

        /// <summary>
        /// Returns the current context's <see cref="CurrentContextName"/> or <c>null</c>
        /// if there's no current context.
        /// </summary>
        public static KubeContextName CurrentContextName => CurrentContext == null ? null : KubeContextName.Parse(CurrentContext.Name);

        /// <summary>
        /// Returns the current <see cref="KubeConfigCluster"/> specified by the <see cref="CurrentContext"/>
        /// from the KubeContext or <c>null</c> when no context is selected or the named cluster does not exist.
        /// </summary>
        public static KubeConfigCluster CurrentCluster
        {
            get
            {
                if (cachedConfig == null || cachedConfig.CurrentContext == null)
                {
                    return null;
                }

                return cachedConfig.GetCluster(cachedConfig.CurrentContext);
            }
        }

        /// <summary>
        /// Returns the current <see cref="KubeConfigUser"/> specified by the <see cref="CurrentContext"/>
        /// from the KubeContext or <c>null</c> when no context is selected or the named user does not exist.
        /// </summary>
        public static KubeConfigUser CurrentUser
        {
            get
            {
                if (cachedConfig == null || cachedConfig.CurrentContext == null)
                {
                    return null;
                }

                return cachedConfig.GetUser(cachedConfig.CurrentContext);
            }
        }

        /// <summary>
        /// Generates a self-signed certificate for arbitrary hostnames, possibly including 
        /// hostnames with wildcards.
        /// </summary>
        /// <param name="hostname">
        /// <para>
        /// The certificate host names.
        /// </para>
        /// <note>
        /// You can use include a <b>"*"</b> to specify a wildcard
        /// certificate like: <b>*.test.com</b>.
        /// </note>
        /// </param>
        /// <param name="bitCount">The certificate key size in bits: one of <b>1024</b>, <b>2048</b>, or <b>4096</b> (defaults to <b>2048</b>).</param>
        /// <param name="validDays">
        /// The number of days the certificate will be valid.  This defaults to 365,000 days
        /// or about 1,000 years.
        /// </param>
        /// <param name="wildcard">
        /// Optionally generate a wildcard certificate for the subdomains of 
        /// <paramref name="hostname"/> or the combination of the subdomains
        /// and the hostname.  This defaults to <see cref="Wildcard.None"/>
        /// which does not generate a wildcard certificate.
        /// </param>
        /// <param name="issuedBy">Optionally specifies the issuer.</param>
        /// <param name="issuedTo">Optionally specifies who/what the certificate is issued for.</param>
        /// <param name="friendlyName">Optionally specifies the certificate's friendly name.</param>
        /// <returns>The new <see cref="X509Certificate2"/>.</returns>
        public static X509Certificate2 CreateSelfSigned(
            string      hostname,
            int         bitCount     = 2048,
            int         validDays    = 365000,
            Wildcard    wildcard     = Wildcard.None,
            string      issuedBy     = null,
            string      issuedTo     = null,
            string      friendlyName = null)
        {
            Covenant.Requires<ArgumentException>(!string.IsNullOrEmpty(hostname), nameof(hostname));
            Covenant.Requires<ArgumentException>(bitCount == 1024 || bitCount == 2048 || bitCount == 4096, nameof(bitCount));
            Covenant.Requires<ArgumentException>(validDays > 1, nameof(validDays));

            if (string.IsNullOrEmpty(issuedBy))
            {
                issuedBy = ".";
            }

            if (string.IsNullOrEmpty(issuedTo))
            {
                issuedTo = hostname;
            }

            var sanBuilder = new SubjectAlternativeNameBuilder();

            switch (wildcard)
            {
                case Wildcard.None:

                    sanBuilder.AddDnsName(hostname);
                    break;

                case Wildcard.SubdomainsOnly:

                    hostname = $"*.{hostname}";
                    sanBuilder.AddDnsName(hostname);
                    break;

                case Wildcard.RootAndSubdomains:

                    sanBuilder.AddDnsName(hostname);
                    sanBuilder.AddDnsName($"*.{hostname}");
                    break;
            }

            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={hostname}");

            using (RSA rsa = RSA.Create(bitCount))
            {
                var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment |
                        X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign |
                        X509KeyUsageFlags.DigitalSignature, true));

                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension());

                request.CertificateExtensions.Add(sanBuilder.Build());

                return request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(validDays)));
            }
        }

        /// <summary>
        /// <para>
        /// Ensures that <b>kubectl</b> tool whose version is at least as great as the Kubernetes
        /// cluster version is installed to the <b>NEONKUBE</b> programs folder by copying the
        /// tool from the cache if necessary.
        /// </para>
        /// <note>
        /// This will probably require elevated privileges.
        /// </note>
        /// <note>
        /// This assumes that <b>kubectl</b> has already been downloaded and cached and also that 
        /// more recent <b>kubectl</b> releases are backwards compatible with older deployed versions
        /// of Kubernetes.
        /// </note>
        /// </summary>
        public static void InstallKubeCtl()
        {
            var hostPlatform      = KubeHelper.HostPlatform;
            var cachedKubeCtlPath = KubeHelper.GetCachedComponentPath(hostPlatform, "kubectl", KubeVersions.Kubernetes);
            var targetPath        = Path.Combine(KubeHelper.InstallFolder);

            switch (hostPlatform)
            {
                case KubeClientPlatform.Windows:

                    targetPath = Path.Combine(targetPath, "kubectl.exe");

                    // Ensure that the KUBECONFIG environment variable exists and includes
                    // the path to the user's [.neonkube] configuration.

                    var kubeConfigVar = Environment.GetEnvironmentVariable("KUBECONFIG");

                    if (string.IsNullOrEmpty(kubeConfigVar))
                    {
                        // The [KUBECONFIG] environment variable doesn't exist so we'll set it.

#pragma warning disable CA1416
                        Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Environment", "KUBECONFIG", KubeConfigPath, RegistryValueKind.ExpandString);
#pragma warning restore CA1416
                        Environment.SetEnvironmentVariable("KUBECONFIG", KubeConfigPath);
                    }
                    else
                    {
                        // The [KUBECONFIG] environment variable exists.  We need to ensure that the
                        // path to our [USER/.neonkube] config is present.  We're also going to ensure
                        // that no paths are duplicated within the variable.

                        var sb    = new StringBuilder();
                        var paths = new HashSet<string>();
                        var found = false;

                        foreach (var path in kubeConfigVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (paths.Contains(path))
                            {
                                // Ignore duplicate paths.

                                continue;
                            }

                            if (path == KubeConfigPath)
                            {
                                found = true;
                            }

                            sb.AppendWithSeparator(path, ";");
                            paths.Add(path);
                        }

                        if (!found)
                        {
                            sb.AppendWithSeparator(KubeConfigPath, ";");
                        }

                        var newKubeConfigVar = sb.ToString();

                        if (newKubeConfigVar != kubeConfigVar)
                        {
#pragma warning disable CA1416
                            Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Environment", "KUBECONFIG", newKubeConfigVar, RegistryValueKind.ExpandString);
#pragma warning restore CA1416
                            Environment.SetEnvironmentVariable("KUBECONFIG", newKubeConfigVar);
                        }
                    }

                    if (!File.Exists(targetPath))
                    {
                        File.Copy(cachedKubeCtlPath, targetPath);
                    }
                    else
                    {
                        // Execute the existing target to obtain its version and update it
                        // to the cached copy if the cluster installed a more recent version
                        // of Kubernetes.

                        // $hack(jefflill): Simple client version extraction

                        var pattern  = "GitVersion:\"v";
                        var response = NeonHelper.ExecuteCapture(targetPath, "version");
                        var pStart   = response.OutputText.IndexOf(pattern);
                        var error    = "Cannot identify existing [kubectl] version.";

                        if (pStart == -1)
                        {
                            throw new NeonKubeException(error);
                        }

                        pStart += pattern.Length;

                        var pEnd = response.OutputText.IndexOf("\"", pStart);

                        if (pEnd == -1)
                        {
                            throw new NeonKubeException(error);
                        }

                        var currentVersionString = response.OutputText.Substring(pStart, pEnd - pStart);

                        if (!Version.TryParse(currentVersionString, out var currentVersion))
                        {
                            throw new NeonKubeException(error);
                        }

                        if (Version.Parse(KubeVersions.Kubernetes) > currentVersion)
                        {
                            // We need to copy the latest version.

                            NeonHelper.DeleteFile(targetPath);
                            File.Copy(cachedKubeCtlPath, targetPath);
                        }
                    }
                    break;

                case KubeClientPlatform.Linux:
                case KubeClientPlatform.Osx:
                default:

                    throw new NotImplementedException($"[{hostPlatform}] support is not implemented.");
            }
        }

        /// <summary>
        /// <para>
        /// Ensures that <b>helm</b> client installed on the workstation version is at least as
        /// great as the requested cluster version is installed to the <b>NEONKUBE</b> programs 
        /// folder by copying the tool from the cache if necessary.
        /// </para>
        /// <note>
        /// This will probably require elevated privileges.
        /// </note>
        /// <note>
        /// This assumes that <b>Helm</b> has already been downloaded and cached and also that 
        /// more recent <b>Helm</b> releases are backwards compatible with older deployed versions
        /// of Tiller.
        /// </note>
        /// </summary>
        public static void InstallWorkstationHelm()
        {
            var hostPlatform   = KubeHelper.HostPlatform;
            var cachedHelmPath = KubeHelper.GetCachedComponentPath(hostPlatform, "helm", KubeVersions.Helm);
            var targetPath     = Path.Combine(KubeHelper.InstallFolder);

            switch (hostPlatform)
            {
                case KubeClientPlatform.Windows:

                    targetPath = Path.Combine(targetPath, "helm.exe");

                    if (!File.Exists(targetPath))
                    {
                        File.Copy(cachedHelmPath, targetPath);
                    }
                    else
                    {
                        // Execute the existing target to obtain its version and update it
                        // to the cached copy if the cluster installed a more recent version
                        // of Kubernetes.

                        // $hack(jefflill): Simple client version extraction

                        var pattern  = "Version:\"v";
                        var response = NeonHelper.ExecuteCapture(targetPath, "version");
                        var pStart   = response.OutputText.IndexOf(pattern);
                        var error    = "Cannot identify existing [helm] version.";

                        if (pStart == -1)
                        {
                            throw new NeonKubeException(error);
                        }

                        pStart += pattern.Length;

                        var pEnd = response.OutputText.IndexOf("\"", pStart);

                        if (pEnd == -1)
                        {
                            throw new NeonKubeException(error);
                        }

                        var currentVersionString = response.OutputText.Substring(pStart, pEnd - pStart);

                        if (!Version.TryParse(currentVersionString, out var currentVersion))
                        {
                            throw new NeonKubeException(error);
                        }
                    }
                    break;

                case KubeClientPlatform.Linux:
                case KubeClientPlatform.Osx:
                default:

                    throw new NotImplementedException($"[{hostPlatform}] support is not implemented.");
            }
        }

        /// <summary>
        /// Executes a <b>kubectl</b> command on the local workstation.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public static ExecuteResponse Kubectl(params object[] args)
        {
            return NeonHelper.ExecuteCapture("kubectl", args);
        }

        /// <summary>
        /// Executes a <b>kubectl port-forward</b> command on the local workstation.
        /// </summary>
        /// <param name="serviceName">The service to forward.</param>
        /// <param name="remotePort">The service port.</param>
        /// <param name="localPort">The local port to forward to.</param>
        /// <param name="namespace">The Kubernetes namespace where the service is running.</param>
        /// <param name="process">The <see cref="Process"/> to use.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public static void PortForward(string serviceName, int remotePort, int localPort, string @namespace, Process process)
        {
            Task.Run(
                () =>
                {
                    NeonHelper.Execute("kubectl",
                        args: new string[]
                        {
                            "--namespace", @namespace,
                            "port-forward",
                            $"svc/{serviceName}",
                            $"{localPort}:{remotePort}"
                        },
                        process: process);
                });
        }

        /// <summary>
        /// Looks up a password given its name.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the password doesn't exist.</exception>
        public static string LookupPassword(string passwordName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(passwordName), nameof(passwordName));

            var path = Path.Combine(PasswordsFolder, passwordName);

            if (!File.Exists(path))
            {
                throw new KeyNotFoundException(passwordName);
            }

            return File.ReadAllText(path).Trim();
        }

        /// <summary>
        /// <para>
        /// Packages files within a folder into an ISO file.
        /// </para>
        /// <note>
        /// This requires Powershell to be installed and this will favor using the version of
        /// Powershell installed along with the neon-cli, if present.
        /// </note>
        /// </summary>
        /// <param name="sourceFolder">Path to the input folder.</param>
        /// <param name="isoPath">Path to the output ISO file.</param>
        /// <param name="label">Optionally specifies a volume label.</param>
        /// <exception cref="ExecuteException">Thrown if the operation failed.</exception>
        public static void CreateIsoFile(string sourceFolder, string isoPath, string label = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourceFolder), nameof(sourceFolder));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(isoPath), nameof(isoPath));
            Covenant.Requires<ArgumentException>(!sourceFolder.Contains('"'), nameof(sourceFolder));    // We don't escape quotes below so we'll
            Covenant.Requires<ArgumentException>(!isoPath.Contains('"'), nameof(isoPath));              // Reject paths including quotes.

            sourceFolder = Path.GetFullPath(sourceFolder);
            label        = label ?? string.Empty;

            // Delete any existing ISO file.

            NeonHelper.DeleteFile(isoPath);

            // Build the ISO.

            var isoBuilder = new CDBuilder();
            var streams    = new List<Stream>();

            try
            {
                isoBuilder.VolumeIdentifier = label;

                foreach (var directory in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
                {
                    var relativePath = directory.Substring(sourceFolder.Length + 1);

                    isoBuilder.AddDirectory(relativePath);
                }

                foreach (var file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
                {
                    var relativePath = file.Substring(sourceFolder.Length + 1);
                    var stream       = File.OpenRead(file);

                    streams.Add(stream);
                    isoBuilder.AddFile(relativePath, stream);
                }

                isoBuilder.Build(isoPath);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream.Dispose();
                }
            }
        }

        /// <summary>
        /// <para>
        /// Creates an ISO file containing the <b>neon-init.sh</b> script that 
        /// will be used for confguring the node on first boot.  This includes disabling
        /// the APT package update services, optionally setting a secure password for the
        /// <b>sysadmin</b> account, and configuring the network interface with a
        /// static IP address.
        /// </para>
        /// <para>
        /// This override has obtains network settings from a <see cref="ClusterDefinition"/>
        /// and <see cref="NodeDefinition"/>.
        /// </para>
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeDefinition">The node definition.</param>
        /// <param name="nodeMtu">
        /// Optionally specifies the MTU to be configured for the node's network interface.
        /// Pass <b>0</b> to configure <see cref="NetConst.DefaultMTU"/> or a value between 
        /// <b>512-9000</b>.
        /// </param>
        /// <param name="newPassword">Optionally specifies the new SSH password to be configured on the node.</param>
        /// <returns>A <see cref="TempFile"/> that references the generated ISO file.</returns>
        /// <remarks>
        /// <para>
        /// The hosting manager will call this for each node being prepared and then
        /// insert the ISO into the node VM's DVD/CD drive before booting the node
        /// for the first time.  The <b>neon-init</b> service configured on
        /// the corresponding node templates will look for this DVD and script and
        /// execute it early during the node boot process.
        /// </para>
        /// <para>
        /// The ISO file reference is returned as a <see cref="TempFile"/>.  The
        /// caller should call <see cref="TempFile.Dispose()"/> when it's done
        /// with the file to ensure that it is deleted.
        /// </para>
        /// </remarks>
        public static TempFile CreateNeonInitIso(
            ClusterDefinition   clusterDefinition,
            NodeDefinition      nodeDefinition,
            int                 nodeMtu     = 0,
            string              newPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));
            Covenant.Requires<ArgumentNullException>(nodeMtu == 0 || (512 <= nodeMtu && nodeMtu <= 9000), nameof(nodeMtu));

            var clusterNetwork = clusterDefinition.Network;

            return CreateNeonInitIso(
                address:        nodeDefinition.Address,
                subnet:         clusterNetwork.PremiseSubnet,
                gateway:        clusterNetwork.Gateway,
                nameServers:    clusterNetwork.Nameservers,
                nodeMtu:        nodeMtu,
                newPassword:    newPassword);
        }

        /// <summary>
        /// <para>
        /// Creates an ISO file containing the <b>neon-init.sh</b> script that 
        /// will be used for confguring the node on first boot.  This includes disabling
        /// the APT package update services, optionally setting a secure password for the
        /// <b>sysadmin</b> account, and configuring the network interface with a
        /// static IP address.
        /// </para>
        /// <para>
        /// This override has explict parameters for configuring the network.
        /// </para>
        /// </summary>
        /// <param name="address">The IP address to be assigned the the VM.</param>
        /// <param name="subnet">The network subnet to be configured.</param>
        /// <param name="gateway">The network gateway to be configured.</param>
        /// <param name="nameServers">The nameserver addresses to be configured.</param>
        /// <param name="nodeMtu">
        /// Optionally specifies the MTU to be configured for the node's network interface.
        /// Pass <b>0</b> to configure <see cref="NetConst.DefaultMTU"/> or a value between 
        /// <b>512-9000</b>.
        /// </param>
        /// <param name="newPassword">Optionally specifies the new SSH password to be configured on the node.</param>
        /// <returns>A <see cref="TempFile"/> that references the generated ISO file.</returns>
        /// <remarks>
        /// <para>
        /// The hosting manager will call this for each node being prepared and then
        /// insert the ISO into the node VM's DVD/CD drive before booting the node
        /// for the first time.  The <b>neon-init</b> service configured on
        /// the corresponding node templates will look for this DVD and script and
        /// execute it early during the node boot process.
        /// </para>
        /// <para>
        /// The ISO file reference is returned as a <see cref="TempFile"/>.  The
        /// caller should call <see cref="TempFile.Dispose()"/> when it's done
        /// with the file to ensure that it is deleted.
        /// </para>
        /// </remarks>
        public static TempFile CreateNeonInitIso(
            string              address,
            string              subnet,
            string              gateway,
            IEnumerable<string> nameServers,
            int                 nodeMtu     = 0,
            string              newPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(address), nameof(address));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(subnet), nameof(subnet));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(gateway), nameof(gateway));
            Covenant.Requires<ArgumentNullException>(nameServers != null, nameof(nameServers));
            Covenant.Requires<ArgumentNullException>(nameServers.Count() > 0, nameof(nameServers));
            Covenant.Requires<ArgumentNullException>(nodeMtu == 0 || (512 <= nodeMtu && nodeMtu <= 9000), nameof(nodeMtu));

            if (nodeMtu == 0)
            {
                nodeMtu = NetConst.DefaultMTU;
            }

            var sbNameservers = new StringBuilder();

            // Generate the [neon-init.sh] script.

            foreach (var nameserver in nameServers)
            {
                sbNameservers.AppendWithSeparator(nameserver.ToString(), ",");
            }

            var changePasswordScript =
$@"
#------------------------------------------------------------------------------
# Change the [sysadmin] user password from the hardcoded [sysadmin0000] password
# to something secure.  We're doing this here before the network is configured means 
# that there will be no time when bad guys can SSH into the node using the insecure
# password.

echo 'sysadmin:{newPassword}' | chpasswd

# Restart [sshd] to pick the change.

systemctl restart ssh
";
            if (String.IsNullOrWhiteSpace(newPassword))
            {
                // Clear the change password script when there's no password.

                changePasswordScript = "\r\n";
            }

            var sbResolvConf = new StringBuilder();

            sbResolvConf.AppendLineLinux(
@"#------------------------------------------------------------------------------
# NEONKUBE explicitly manages the [/etc/resolv.conf] file to prevent DHCP from
# messing with this even though we're using a STATIC netplan config.  We delete
# the original symlinked file during cluster setup and replace it with this file.
");

            foreach (var nameServer in nameServers)
            {
                sbResolvConf.AppendLineLinux($"nameserver {nameServer}");
            }

            var nodePrepScript =
$@"# This script is called by the [neon-init] service when the prep DVD
# is inserted on boot.  This script handles setting a secure SSH password
# as well as configuring the network interface to a static IP address.
#
# The first parameter will be passed as the path where the DVD is mounted.

mountFolder=${{1}}

#------------------------------------------------------------------------------
# Sleep for a bit in an attempt to ensure that the system is actually ready.
#
# https://github.com/nforgeio/neonKUBE/issues/980

sleep 5
{changePasswordScript}
#------------------------------------------------------------------------------
# Configure the network.

echo ""Configure network: {address}""

# Make a backup copy of any original netplan files to the [/etc/neon-init/netplan-backup]
# folder so it will be possible to restore these if we need to reset the [neon-init] state.

mkdir -p /etc/netplan
mkdir -p /etc/neon-init/netplan-backup
cp -r /etc/netplan/* /etc/neon-init/netplan-backup

# Remove any existing netplan files so we can update the configuration.

rm -rf /etc/netplan/*

cat <<EOF > /etc/netplan/static.yaml
# Static network configuration is initialized during first boot by the 
# [neon-init] service from a virtual ISO inserted during cluster prepare.

network:
  version: 2
  renderer: networkd
  ethernets:
    eth0:
     dhcp4: no
     dhcp6: no
     mtu: {nodeMtu}
     addresses: [{address}/{NetworkCidr.Parse(subnet).PrefixLength}]
     routes:
     - to: default
       via: {gateway}
     nameservers:
       addresses: [{sbNameservers}]
EOF

# Replace [/etc/resolv.conf] with our manually managed file.

rm /etc/resolv.conf
cat <<EOF > /etc/resolv.conf
{sbResolvConf}
EOF
chmod 644 /etc/resolv.conf

# Restart the network.

echo ""Restart network""

while true; do

    netplan apply
    if [ ! $? ] ; then
        echo ""ERROR: Network restart failed.""
        sleep 1
    else
        break
fi

done

echo ""Node is prepared.""

# Enable password login.

sed -iE 's/#*PasswordAuthentication.*/PasswordAuthentication yes/' /etc/ssh/sshd_config

# Restart SSHD to pick up the changes.

systemctl restart ssh

exit 0
";
            nodePrepScript = NeonHelper.ToLinuxLineEndings(nodePrepScript);

            // Create an ISO that includes the script and return the ISO TempFile.
            //
            // NOTE:
            //
            // that the ISO needs to be created in an unencrypted folder so that Hyper-V 
            // can mount it to a VM.  By default, [neon-cli] will redirect the [TempFolder] 
            // and [TempFile] classes locate their folder and files here:
            //
            //      /USER/.neonkube/...     - which is encrypted on Windows

            var orgTempPath = Path.GetTempPath();

            using (var tempFolder = new TempFolder(folder: orgTempPath))
            {
                File.WriteAllText(Path.Combine(tempFolder.Path, "neon-init.sh"), nodePrepScript);

                // Note that the ISO needs to be created in an unencrypted folder
                // (not /USER/neonkube/...) so that Hyper-V can mount it to a VM.
                //
                var isoFile = new TempFile(suffix: ".iso", folder: orgTempPath);

                KubeHelper.CreateIsoFile(tempFolder.Path, isoFile.Path, "cidata");

                return isoFile;
            }
        }

        /// <summary>
        /// Returns the path to the <b>ssh-keygen.exe</b> tool to be used for creating
        /// and managing SSH keys.
        /// </summary>
        /// <returns>The fully qualified path to the executable.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the executable could not be found.</exception>
        private static string GetSshKeyGenPath()
        {
            // The version of [ssh-keygen.exe] included with later versions of Windows doesn't
            // work for us because it cannot create a key without a passphrase when called from
            // a script or other process.
            //
            // We're going to use a version of this tool deployed with the Git tools for Windows.
            // This will be installed with NEONDESKTOP and is also available as part of the
            // NEONKUBE Git repo as a fall back for Neon developers that haven't installed 
            // the desktop yet.

            // Look for the installed version first.

            var defaultPath = InstallFolder != null ? Path.Combine(InstallFolder, "ssh", "ssh-keygen.exe") : null;

            if (defaultPath != null && File.Exists(defaultPath))
            {
                return Path.GetFullPath(defaultPath);
            }

            // Fall back to the executable in our git repo (for developers).

            var repoFolder   = Environment.GetEnvironmentVariable("NK_ROOT");
            var fallbackPath = repoFolder != null ? Path.Combine(repoFolder, "External", "ssh", "ssh-keygen.exe") : null;

            if (fallbackPath != null && File.Exists(fallbackPath))
            {
                return Path.GetFullPath(fallbackPath);
            }

#if DEBUG
            throw new FileNotFoundException($"Cannot locate [ssh-keygen.exe] at [{defaultPath}] or [{fallbackPath}].");
#else
            throw new FileNotFoundException($"Cannot locate [ssh-keygen.exe] at [{defaultPath}].");
#endif
        }

        /// <summary>
        /// Creates a SSH key for a NEONKUBE cluster.
        /// </summary>
        /// <param name="clusterName">The cluster name.</param>
        /// <param name="userName">Specifies the user name.</param>
        /// <returns>A <see cref="KubeSshKey"/> holding the public and private parts of the key.</returns>
        public static KubeSshKey GenerateSshKey(string clusterName, string userName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName), nameof(clusterName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(userName), nameof(userName));

            var sshKeyGenPath = GetSshKeyGenPath();

            using (var tempFolder = new TempFolder(TempFolder))
            {
                //-------------------------------------------------------------
                // Generate and load the public and private keys.

                var result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-t", "rsa",
                        "-b", "2048",
                        "-N", "''",
                        "-C", $"{userName}@{clusterName}",
                        "-f", Path.Combine(tempFolder.Path, "key")
                    });

                if (result.ExitCode != 0)
                {
                    throw new NeonKubeException("Cannot generate SSH key:\r\n\r\n" + result.AllText);
                }

                var publicPUB      = NeonHelper.ToLinuxLineEndings(File.ReadAllText(Path.Combine(tempFolder.Path, "key.pub")));
                var privateOpenSSH = NeonHelper.ToLinuxLineEndings(File.ReadAllText(Path.Combine(tempFolder.Path, "key")));

                //-------------------------------------------------------------
                // We also need the public key in PEM format.

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-f", Path.Combine(tempFolder.Path, "key.pub"),
                        "-e",
                        "-m", "pem",
                    });

                if (result.ExitCode != 0)
                {
                    throw new NeonKubeException("Cannot convert SSH public key to PEM:\r\n\r\n" + result.AllText);
                }

                var publicOpenSSH = NeonHelper.ToLinuxLineEndings(result.OutputText);

                //-------------------------------------------------------------
                // Also convert the public key to SSH2 (RFC 4716).

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-f", Path.Combine(tempFolder.Path, "key.pub"),
                        "-e",
                    });

                if (result.ExitCode != 0)
                {
                    throw new NeonKubeException("Cannot convert SSH public key to SSH2:\r\n\r\n" + result.AllText);
                }

                var publicSSH2 = NeonHelper.ToLinuxLineEndings(result.OutputText);

                // Strip out the comment header line if one was added during the conversion.

                var sbPublicSSH2 = new StringBuilder();

                using (var reader = new StringReader(publicSSH2))
                {
                    foreach (var line in reader.Lines())
                    {
                        if (!line.StartsWith("Comment: "))
                        {
                            sbPublicSSH2.AppendLine(line);
                        }
                    }
                }

                publicSSH2 = NeonHelper.ToLinuxLineEndings(sbPublicSSH2.ToString());

                //-------------------------------------------------------------
                // We need the private key as PEM

                File.Copy(Path.Combine(tempFolder.Path, "key"), Path.Combine(tempFolder.Path, "key.pem"));

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-f", Path.Combine(tempFolder.Path, "key.pem"),
                        "-p",
                        "-P", "''",
                        "-N", "''",
                        "-m", "pem",
                    });

                if (result.ExitCode != 0)
                {
                    throw new NeonKubeException("Cannot convert SSH private key to PEM:\r\n\r\n" + result.AllText);
                }

                var privatePEM = NeonHelper.ToLinuxLineEndings(File.ReadAllText(Path.Combine(tempFolder.Path, "key.pem")));

                //-------------------------------------------------------------
                // We need to obtain the MD5 fingerprint from the public key.

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-l",
                        "-f", Path.Combine(tempFolder.Path, "key.pub"),
                        "-E", "md5",
                    });

                if (result.ExitCode != 0)
                {
                    throw new NeonKubeException("Cannot generate SSH public key MD5 fingerprint:\r\n\r\n" + result.AllText);
                }

                var fingerprintMd5 = NeonHelper.ToLinuxLineEndings(result.OutputText.Trim());

                //-------------------------------------------------------------
                // We also need the SHA256 fingerprint.

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-l",
                        "-f", Path.Combine(tempFolder.Path, "key.pub"),
                        "-E", "sha256"
                    });

                if (result.ExitCode != 0)
                {
                    throw new NeonKubeException("Cannot generate SSH public key SHA256 fingerprint:\r\n\r\n" + result.AllText);
                }

                var fingerprintSha2565 = NeonHelper.ToLinuxLineEndings(result.OutputText.Trim());

                //-------------------------------------------------------------
                // Return the key information.

                return new KubeSshKey()
                {
                    PublicPUB         = publicPUB,
                    PublicOpenSSH     = publicOpenSSH,
                    PublicSSH2        = publicSSH2,
                    PrivateOpenSSH    = privateOpenSSH,
                    PrivatePEM        = privatePEM,
                    FingerprintMd5    = fingerprintMd5,
                    FingerprintSha256 = fingerprintSha2565
                };
            }
        }

        /// <summary>
        /// Generates a unique cluster ID.
        /// </summary>
        /// <returns>The generated cluster ID.</returns>
        public static string GenerateClusterId()
        {
            return Guid.NewGuid().ToFoldedHex(dashes: true);
        }

        /// <summary>
        /// <para>
        /// Returns the fixed SSH key shared by all NEONDESKTOP clusters.
        /// </para>
        /// <note>
        /// This isn't really a security issue because NEONDESKTOP clusters are not
        /// reachable from outside the machine they're deployed on and also because
        /// the NEONDESKTOP cluster is not intended to host production workloads.
        /// </note>
        /// </summary>
        /// <returns>The <see cref="KubeSshKey"/>.</returns>
        public static KubeSshKey GetBuiltinDesktopSshKey()
        {
            // $note(jefflill):
            //
            // This key was generated using the NEONCLOUD neon-image tool via:
            //
            //      neon-image sshkey neon-desktop sysadmin
            //
            // SSH keys don't have an expiration so this key could potentionally work
            // forever, as long as the encryption algorithms are still supported.

            var keyYaml =
@"
publicPUB: |
  ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDEslLqqMimG0SIrQFs+nPl7Ah8TSoKRBJuPwhVkefMzUol1Rvlm7Yc0kRwhBTQkS7H9g51CENEo42D8trorj3d1rRTGpiOjCuxvMg71l0xDPN3Kjovde2TT3rDp0apqT24+SGu8xupbd2cmsK13eOfEL4RxufIyJSnpc7sxGM7MmbnFC8fDrE3yxZTA4+Qa1bUpM66XD569mCSr4l0zSQcVkGxlPCYIVT0WLWxf1Jbe+ZVvMQrvQ5S5tBejvp7O+SPZXONewzX+nOWhyOuMin+PELMPQiYAyRf8DpQ4l/e1h0MnQcAv09TBk9H4bJefGgPfNcDB5OAN6QafT6Nwfyn sysadmin@neon-desktop
publicOpenSSH: |
  -----BEGIN RSA PUBLIC KEY-----
  MIIBCgKCAQEAxLJS6qjIphtEiK0BbPpz5ewIfE0qCkQSbj8IVZHnzM1KJdUb5Zu2
  HNJEcIQU0JEux/YOdQhDRKONg/La6K493da0UxqYjowrsbzIO9ZdMQzzdyo6L3Xt
  k096w6dGqak9uPkhrvMbqW3dnJrCtd3jnxC+EcbnyMiUp6XO7MRjOzJm5xQvHw6x
  N8sWUwOPkGtW1KTOulw+evZgkq+JdM0kHFZBsZTwmCFU9Fi1sX9SW3vmVbzEK70O
  UubQXo76ezvkj2VzjXsM1/pzlocjrjIp/jxCzD0ImAMkX/A6UOJf3tYdDJ0HAL9P
  UwZPR+GyXnxoD3zXAweTgDekGn0+jcH8pwIDAQAB
  -----END RSA PUBLIC KEY-----
publicSSH2: |
  ---- BEGIN SSH2 PUBLIC KEY ----
  AAAAB3NzaC1yc2EAAAADAQABAAABAQDEslLqqMimG0SIrQFs+nPl7Ah8TSoKRBJuPwhVke
  fMzUol1Rvlm7Yc0kRwhBTQkS7H9g51CENEo42D8trorj3d1rRTGpiOjCuxvMg71l0xDPN3
  Kjovde2TT3rDp0apqT24+SGu8xupbd2cmsK13eOfEL4RxufIyJSnpc7sxGM7MmbnFC8fDr
  E3yxZTA4+Qa1bUpM66XD569mCSr4l0zSQcVkGxlPCYIVT0WLWxf1Jbe+ZVvMQrvQ5S5tBe
  jvp7O+SPZXONewzX+nOWhyOuMin+PELMPQiYAyRf8DpQ4l/e1h0MnQcAv09TBk9H4bJefG
  gPfNcDB5OAN6QafT6Nwfyn
  ---- END SSH2 PUBLIC KEY ----
privateOpenSSH: |
  -----BEGIN OPENSSH PRIVATE KEY-----
  b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABFwAAAAdzc2gtcn
  NhAAAAAwEAAQAAAQEAxLJS6qjIphtEiK0BbPpz5ewIfE0qCkQSbj8IVZHnzM1KJdUb5Zu2
  HNJEcIQU0JEux/YOdQhDRKONg/La6K493da0UxqYjowrsbzIO9ZdMQzzdyo6L3Xtk096w6
  dGqak9uPkhrvMbqW3dnJrCtd3jnxC+EcbnyMiUp6XO7MRjOzJm5xQvHw6xN8sWUwOPkGtW
  1KTOulw+evZgkq+JdM0kHFZBsZTwmCFU9Fi1sX9SW3vmVbzEK70OUubQXo76ezvkj2VzjX
  sM1/pzlocjrjIp/jxCzD0ImAMkX/A6UOJf3tYdDJ0HAL9PUwZPR+GyXnxoD3zXAweTgDek
  Gn0+jcH8pwAAA9Bv6c9eb+nPXgAAAAdzc2gtcnNhAAABAQDEslLqqMimG0SIrQFs+nPl7A
  h8TSoKRBJuPwhVkefMzUol1Rvlm7Yc0kRwhBTQkS7H9g51CENEo42D8trorj3d1rRTGpiO
  jCuxvMg71l0xDPN3Kjovde2TT3rDp0apqT24+SGu8xupbd2cmsK13eOfEL4RxufIyJSnpc
  7sxGM7MmbnFC8fDrE3yxZTA4+Qa1bUpM66XD569mCSr4l0zSQcVkGxlPCYIVT0WLWxf1Jb
  e+ZVvMQrvQ5S5tBejvp7O+SPZXONewzX+nOWhyOuMin+PELMPQiYAyRf8DpQ4l/e1h0MnQ
  cAv09TBk9H4bJefGgPfNcDB5OAN6QafT6NwfynAAAAAwEAAQAAAQArUJmx0zlcWuTctDx8
  IysilrfHp7Z6TENCw96x+U9yakLJ0gQyq/eOoT8xB+UNiOskXasRWqB1nQ6s3+4VD0nQcF
  eFdXXi7jsxCMGPa8VZ5+A1fbcSfIW0yuvd6hhFhF9zPGmOfTq6NNd4hRwbsKFPhgBVKdgg
  /wq9YGYQ/a5cenoXQWY7v8xzvUZ5tpnx2d5hr1/SXL8BcnCVD7X95s4FYl/36+P+UUibPn
  jXzVskvPYB0r9RNvOlo7GUiDmjKVDZhAwaklR653fDZCdl1O5//Ky8+0Vj5qTT1yt+F2d9
  vYgoA4wQbkr01/0X9Q/LUrBPHmWl3ovje3q2NHND8QUBAAAAgEuj0eHjt2OPOm80OCz+BP
  81ruqOXMoZRwl5OtY0RUoGZPggO0MLPKDfCqJHTCMTr8IMYMQKJgoFxI6IGirpml+n6/vY
  WBQlxYQNPvWw47qqTLhHudDU/dlaG1JybVM0r19vX4lUbATTIbetGERh4QimePFXyqn5ZY
  bGTCoXhYoJAAAAgQDjnaGcxfeUExBPE6GekKiCXl4ixdJM5VBWd7Qxsfcira0pZ+9ZGZv9
  TrYSTj3OntwkjupjvEuiPKOAdsLPcopVxC5zXvFkuxHoQ7tUgkHcdjP3QAxNq33VEicSkV
  qTJsEuRXJMtw1CKpaWaNNYcHYchAQtHPOhUefz5Nhcwts9MwAAAIEA3TmiTANFg4mYQXKF
  Wm2HgjwiURqhedjjBf+7AT2AFRVAjnSTgApY8dHI4W5/nhgm8gvqw7Mp0J4UWtQKWxSPtn
  o9tmoqmI1e2OVRAqYLMDYYh9QjEKZ9NUpzdJGAUk3YqNyHdmLFbrv3onNz0bE+kVS33pzH
  kaXzMnM/G9NW+r0AAAAVc3lzYWRtaW5AbmVvbi1kZXNrdG9wAQIDBAUG
  -----END OPENSSH PRIVATE KEY-----
privatePEM: |
  -----BEGIN RSA PRIVATE KEY-----
  MIIEogIBAAKCAQEAxLJS6qjIphtEiK0BbPpz5ewIfE0qCkQSbj8IVZHnzM1KJdUb
  5Zu2HNJEcIQU0JEux/YOdQhDRKONg/La6K493da0UxqYjowrsbzIO9ZdMQzzdyo6
  L3Xtk096w6dGqak9uPkhrvMbqW3dnJrCtd3jnxC+EcbnyMiUp6XO7MRjOzJm5xQv
  Hw6xN8sWUwOPkGtW1KTOulw+evZgkq+JdM0kHFZBsZTwmCFU9Fi1sX9SW3vmVbzE
  K70OUubQXo76ezvkj2VzjXsM1/pzlocjrjIp/jxCzD0ImAMkX/A6UOJf3tYdDJ0H
  AL9PUwZPR+GyXnxoD3zXAweTgDekGn0+jcH8pwIDAQABAoIBACtQmbHTOVxa5Ny0
  PHwjKyKWt8entnpMQ0LD3rH5T3JqQsnSBDKr946hPzEH5Q2I6yRdqxFaoHWdDqzf
  7hUPSdBwV4V1deLuOzEIwY9rxVnn4DV9txJ8hbTK693qGEWEX3M8aY59Oro013iF
  HBuwoU+GAFUp2CD/Cr1gZhD9rlx6ehdBZju/zHO9Rnm2mfHZ3mGvX9JcvwFycJUP
  tf3mzgViX/fr4/5RSJs+eNfNWyS89gHSv1E286WjsZSIOaMpUNmEDBqSVHrnd8Nk
  J2XU7n/8rLz7RWPmpNPXK34XZ329iCgDjBBuSvTX/Rf1D8tSsE8eZaXei+N7erY0
  c0PxBQECgYEA452hnMX3lBMQTxOhnpCogl5eIsXSTOVQVne0MbH3Iq2tKWfvWRmb
  /U62Ek49zp7cJI7qY7xLojyjgHbCz3KKVcQuc17xZLsR6EO7VIJB3HYz90AMTat9
  1RInEpFakybBLkVyTLcNQiqWlmjTWHB2HIQELRzzoVHn8+TYXMLbPTMCgYEA3Tmi
  TANFg4mYQXKFWm2HgjwiURqhedjjBf+7AT2AFRVAjnSTgApY8dHI4W5/nhgm8gvq
  w7Mp0J4UWtQKWxSPtno9tmoqmI1e2OVRAqYLMDYYh9QjEKZ9NUpzdJGAUk3YqNyH
  dmLFbrv3onNz0bE+kVS33pzHkaXzMnM/G9NW+r0CgYAp85Co43fpK8ZSvMyJ/CGC
  vb/d6tYC5DT1auSkUCe7lYUX35cmteihPFOkdhVAMtliR5D9xuOtyD1eXQU01OiY
  PCtPik01gqEfTPSG8+cNqh+Tz5M08Ymkrs7SxkWKX5c1XwldCFQCQPU2TaW+ZCPw
  x4g5hF+G+SCmPCSAnE1qLwKBgFnLL/YcidWnPtapzjjzJkKVd/Rlk89qWlOwBk6t
  kNR96NMpvEkHaizVUu01tbUM5pnufl7q1PkpgOeRE5b+lIqjuXLWSu3ay/nLsoMZ
  tIbgHjrbv1Pd0AqWaqCRAn3lvSBlStKhqrOUtiIJLKSbheLleTBxgIu8ySbcImx/
  7tkdAoGAS6PR4eO3Y486bzQ4LP4E/zWu6o5cyhlHCXk61jRFSgZk+CA7Qws8oN8K
  okdMIxOvwgxgxAomCgXEjogaKumaX6fr+9hYFCXFhA0+9bDjuqpMuEe50NT92Vob
  UnJtUzSvX29fiVRsBNMht60YRGHhCKZ48VfKqfllhsZMKheFigk=
  -----END RSA PRIVATE KEY-----
fingerprint-SHA256: |-
  2048 SHA256:3RSnLcWeie26esBIDmfy4LrHEkGf+gQYIjL13KHcGbg sysadmin@neon-desktop (RSA)
fingerprint-MD5: |-
  2048 MD5:a7:e5:58:d6:9f:88:b8:3e:b7:81:f9:f3:d6:8c:9c:98 sysadmin@neon-desktop (RSA)
passphrase: 
";
            return NeonHelper.YamlDeserialize<KubeSshKey>(keyYaml);
        }

        /// <summary>
        /// <para>
        /// Ensures that at least one cluster node is enabled for cluster ingress
        /// network traffic.
        /// </para>
        /// <note>
        /// It is possible for the user to have set <see cref="NodeDefinition.Ingress"/>
        /// to <c>false</c> for all nodes.  We're going to pick a reasonable set of
        /// nodes in this case.  I there are 3 or more workers, then only the workers
        /// will receive traffic, otherwise all nodes will receive traffic.
        /// </note>
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        public static void EnsureIngressNodes(ClusterDefinition clusterDefinition)
        {
            if (!clusterDefinition.Nodes.Any(node => node.Ingress))
            {
                var workerCount = clusterDefinition.Workers.Count();

                if (workerCount < 3)
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Ingress = true;
                    }
                }
                else
                {
                    foreach (var worker in clusterDefinition.Workers)
                    {
                        worker.Ingress = true;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the OpenSSH configuration file used for cluster nodes.
        /// </summary>
        public static string OpenSshConfig =>
@"# FILE:          sshd_config
# CONTRIBUTOR: Jeff Lill
# COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the ""License"");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
# http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an ""AS IS"" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# This file is written to NEONKUBE nodes during cluster preparation.  The
# settings below were captured from the OpenSSH version installed with
# Ubuntu-22.04:
#
# OpenSSH_8.9p1 Ubuntu-3, OpenSSL 3.0.2 15 Mar 2022
#
# The only change we made was to move the include statement from the top
# to the bottom of this file:
#
# Include /etc/ssh/sshd_config.d/*.conf
#
# This allows the sub-config files to be able to override all of the settings
# here.  Cluster preparaton works by writing a sub-config file with our custom
# settings:
#
#       /etc/ssh/sshd_config.d/60-neonkube.conf

###############################################################################
# Default OpenSSH config file                                                 #
###############################################################################

# This is the sshd server system-wide configuration file.  See
# sshd_config(5) for more information.

# This sshd was compiled with PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games

# The strategy used for options in the default sshd_config shipped with
# OpenSSH is to specify options with their default value where
# possible, but leave them commented.  Uncommented options override the
# default value.

# Port 22
# AddressFamily any
# ListenAddress 0.0.0.0
# ListenAddress ::

# HostKey /etc/ssh/ssh_host_rsa_key
# HostKey /etc/ssh/ssh_host_ecdsa_key
# HostKey /etc/ssh/ssh_host_ed25519_key

# Ciphers and keying
# RekeyLimit default none

# Logging
# SyslogFacility AUTH
# LogLevel INFO

# Authentication:

# LoginGraceTime 2m
# PermitRootLogin prohibit-password
# StrictModes yes
# MaxAuthTries 6
# MaxSessions 10

PubkeyAuthentication yes
PubkeyAcceptedKeyTypes +ssh-rsa
HostKeyAlgorithms +ssh-rsa

# Expect .ssh/authorized_keys2 to be disregarded by default in future.
# AuthorizedKeysFile     .ssh/authorized_keys .ssh/authorized_keys2

# AuthorizedPrincipalsFile none

# AuthorizedKeysCommand none
# AuthorizedKeysCommandUser nobody

# For this to work you will also need host keys in /etc/ssh/ssh_known_hosts
# HostbasedAuthentication no
# Change to yes if you don't trust ~/.ssh/known_hosts for
# HostbasedAuthentication
# IgnoreUserKnownHosts no
# Don't read the user's ~/.rhosts and ~/.shosts files
# IgnoreRhosts yes

# To disable tunneled clear text passwords, change to no here!
PasswordAuthentication yes
# PermitEmptyPasswords no

# Change to yes to enable challenge-response passwords (beware issues with
# some PAM modules and threads)
KbdInteractiveAuthentication no

# Kerberos options
# KerberosAuthentication no
# KerberosOrLocalPasswd yes
# KerberosTicketCleanup yes
# KerberosGetAFSToken no

# GSSAPI options
# GSSAPIAuthentication no
# GSSAPICleanupCredentials yes
# GSSAPIStrictAcceptorCheck yes
# GSSAPIKeyExchange no

# Set this to 'yes' to enable PAM authentication, account processing,
# and session processing. If this is enabled, PAM authentication will
# be allowed through the KbdInteractiveAuthentication and
# PasswordAuthentication.  Depending on your PAM configuration,
# PAM authentication via KbdInteractiveAuthentication may bypass
# the setting of ""PermitRootLogin without-password"".
# If you just want the PAM account and session checks to run without
# PAM authentication, then enable this but set PasswordAuthentication
# and KbdInteractiveAuthentication to 'no'.
UsePAM yes

# AllowAgentForwarding yes
# AllowTcpForwarding yes
# GatewayPorts no
X11Forwarding yes
# X11DisplayOffset 10
# X11UseLocalhost yes
# PermitTTY yes
PrintMotd no
# PrintLastLog yes
# TCPKeepAlive yes
# PermitUserEnvironment no
# Compression delayed
# ClientAliveInterval 0
# ClientAliveCountMax 3
# UseDNS no
# PidFile /run/sshd.pid
# MaxStartups 10:30:100
# PermitTunnel no
# ChrootDirectory none
# VersionAddendum none

# no default banner path
# Banner none

# Allow client to pass locale environment variables
AcceptEnv LANG LC_*

# override default of no subsystems
Subsystem sftp  /usr/lib/openssh/sftp-server

# Example of overriding settings on a per-user basis
# Match User anoncvs
# X11Forwarding no
# AllowTcpForwarding no
# PermitTTY no
# ForceCommand cvs server

###############################################################################
# NEONKUBE customization: relocated from the top of the original file         #
###############################################################################

Include /etc/ssh/sshd_config.d/*.conf
";

        /// <summary>
        /// Returns the contexts of the OpenSSH sub-config file to deployed during
        /// as node images are created or when the cluster nodes are provisioned 
        /// to <b>/etc/ssh/sshd_config.d/20-neonkube.conf</b> to customize OpenSSH.
        /// </summary>
        /// <param name="allowPasswordAuth">Enable password authentication.</param>
        public static string GetOpenSshPrepareSubConfig(bool allowPasswordAuth)
        {
            var allowPasswordAuthValue = allowPasswordAuth ? "yes" : "no";

            return
$@"# FILE:         /etc/ssh/sshd_config.d/60-neonkube.conf
# CONTRIBUTOR: Jeff Lill
# COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the ""License"");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
# http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an ""AS IS"" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# This file is written to NEONKUBE nodes during cluster preparation
# to customize OpenSSH.
#
# See the sshd_config(5) manpage for details

# Authentication

PermitRootLogin no
usePAM {allowPasswordAuthValue}
PasswordAuthentication {allowPasswordAuthValue}
AuthorizedKeysFile %h/.ssh/authorized_keys

#------------------------------------------------------------------------------
# Interfactive login

PrintMotd no

#------------------------------------------------------------------------------
# Networking

AllowTcpForwarding no

# Allow connections to be idle for up to an 10 minutes (600 seconds)
# before terminating them.  This configuration pings the client every
# 30 seconds for up to 20 times without a response:
#
#   30*20 = 600 seconds

ClientAliveInterval 30
ClientAliveCountMax 20
TCPKeepAlive yes
";
        }

        /// <summary>
        /// Downloads a multi-part node image to a local folder.
        /// </summary>
        /// <param name="imageUri">The node image multi-part download information URI.</param>
        /// <param name="imagePath">The local path where the image will be written.</param>
        /// <param name="progressAction">Optional progress action that will be called with operation percent complete.</param>
        /// <param name="strictCheck">
        /// <para>
        /// Optionally used to disable a slow but more comprehensive check of any existing file.
        /// When this is disabled and the download file already exists along with its MD5 hash file,
        /// the method will assume that the existing file matches when the file size is the same
        /// as specified in the manifest and manifest overall MD5 matches the local MD5 file.
        /// </para>
        /// <para>
        /// Otherwise when <paramref name="strictCheck"/> is <c>true</c>, this method will need to 
        /// compute the MD5 hashes for the existing file parts and compare those to the part MD5
        /// hashes in the manifest, which can take quite a while for large files.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c>.
        /// </para>
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The path to the downloaded file.</returns>
        /// <exception cref="SocketException">Thrown for network errors.</exception>
        /// <exception cref="HttpException">Thrown for HTTP network errors.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation was cancelled.</exception>
        /// <remarks>
        /// <para>
        /// This checks to see if the target file already exists and will download
        /// only what's required to update the file to match the source.  This means
        /// that partially completed downloads can restart essentially where they
        /// left off.
        /// </para>
        /// </remarks>
        public static async Task<string> DownloadNodeImageAsync(
            string                      imageUri, 
            string                      imagePath,
            DownloadProgressDelegate    progressAction    = null,
            bool                        strictCheck       = true,
            CancellationToken           cancellationToken = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(imageUri != null, nameof(imageUri));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(imagePath), nameof(imagePath));

            var imageFolder = Path.GetDirectoryName(imagePath);

            Directory.CreateDirectory(imageFolder);

            // Download the URI and parse a [DownloadManifest] instance from it.

            using (var client = new HttpClient())
            {
                var request     = new HttpRequestMessage(HttpMethod.Get, imageUri);
                var response    = await client.SendSafeAsync(request, cancellationToken: cancellationToken);
                var contentType = response.Content.Headers.ContentType.MediaType;

                if (!string.Equals(contentType, DeploymentHelper.DownloadManifestContentType, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new NeonKubeException($"[{imageUri}] has unsupported [Content-Type={contentType}].  [{DeploymentHelper.DownloadManifestContentType}] is expected.");
                }

                var jsonText = await response.Content.ReadAsStringAsync();
                var manifest = NeonHelper.JsonDeserialize<DownloadManifest>(jsonText);

                // Download the multi-part file.

                return await DeploymentHelper.DownloadMultiPartAsync(manifest, imagePath, progressAction, strictCheck: strictCheck, cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Returns the path to the a tool binary to be used by <b>neon-cli</b>.
        /// </summary>
        /// <param name="installFolder">Path to the tool installation folder.</param>
        /// <param name="toolName">The requested tool name, currently <b>helm</b> is supported.</param>
        /// <param name="toolChecker">Callback taking the the tool path as a parameter and returning <c>true</c> when the tool version matches what's required.</param>
        /// <param name="userToolsFolder">
        /// Optionally specifies that instead of downloading missing tool binaries to <paramref name="installFolder"/>,
        /// the method will download the file to <see cref="ToolsFolder"/>.
        /// </param>
        /// <param name="toolUriRetriever">Callback that returns the URI to be used to download the tool.</param>
        /// <returns>The fully qualified tool path.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the tool cannot be located.</exception>
        /// <remarks>
        /// <para>
        /// If the <paramref name="installFolder"/> folder and the binary exist then we'll simply
        /// return the tool path when <paramref name="userToolsFolder"/><c>=true</c> and verify 
        /// that tool version is correct when <paramref name="userToolsFolder"/><c>=false</c>.
        /// </para>
        /// <para>
        /// If the <paramref name="installFolder"/> or binary does not exist, then the user is probably
        /// a developer running an uninstalled version of the tool, perhaps in the debugger.  In this case, 
        /// we're going to download the binaries to <paramref name="installFolder"/> by default or to 
        /// <see cref="ToolsFolder"/> when <paramref name="userToolsFolder"/><c>=true</c>.
        /// </para>
        /// </remarks>
        public static string GetToolPath(string installFolder, string toolName, Func<string, bool> toolChecker, Func<string> toolUriRetriever, bool userToolsFolder = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(installFolder), nameof(installFolder));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(toolName), nameof(toolName));
            Covenant.Requires<ArgumentNullException>(toolChecker != null, nameof(toolChecker));
            Covenant.Requires<ArgumentNullException>(toolUriRetriever != null, nameof(toolUriRetriever));

            // Ensure that the install folder actually exists.

            Directory.CreateDirectory(installFolder);

            // Determine the full tool file name.

            string extension;

            if (NeonHelper.IsWindows)
            {
                extension = ".exe";
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                extension = string.Empty;
            }
            else
            {
                throw new NotSupportedException(NeonHelper.OSDescription);
            }

            var toolFile = $"{toolName}{extension}";

            // If the tool exists in the standard install location, then simply return its
            // path.  We're going to assume that the tool version is correct in this case
            // [userToolsFolder=true].
            //
            // If the tool exists and [userToolsFolder==false], we're going to verify its
            // version and return the tool path when that's correct.
            // 
            // Otherwise if the tool doesn't exist or its version is incorrect, we're
            // going to drop thru to download the binaries to [installFolder] when
            // [userToolsFolder=false] or to [KubeHelper.ToolsFolder] when
            // [userToolsFolder=false].

            var toolPath = Path.Combine(installFolder, toolFile);

            if (File.Exists(toolPath) && (userToolsFolder || toolChecker(toolPath)))
            {
                return toolPath;
            }

            // The tool doesn't exist in the standard install location or isn't the correct
            // version, so we'll check for it in the user's tool cache when enabled.

            if (userToolsFolder)
            {
                toolPath = Path.Combine(KubeHelper.ToolsFolder, toolFile);

                if (File.Exists(toolPath))
                {
                    // If the cached tool version is correct (by calling the tool checker callback),
                    // then return it's path.

                    if (toolChecker(toolPath))
                    {
                        return toolPath;
                    }
                }
            }

            // We'll land here when there's no cached binary or if its version is not correct.
            // Any existing binary will be deleted and then we'll attempt to download a new copy.
            //
            // NOTE: We're going to require that the URI being downloaded is a TAR.GZ or a .ZIP file.

            var toolUri      = new Uri(toolUriRetriever());
            var downloadPath = Path.Combine(KubeHelper.TempFolder, $"download-{Guid.NewGuid().ToString("d")}.tar.gz");

            Covenant.Assert(toolUri.AbsolutePath.EndsWith(".tar.gz", StringComparison.InvariantCultureIgnoreCase) ||
                            toolUri.AbsolutePath.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase), 
                            "Expecting a TAR.GZ or .ZIP file.");

            NeonHelper.DeleteFile(toolPath);

            using (var httpClient = new HttpClient())
            {
                var response = httpClient.GetSafeAsync(toolUri, completionOption: HttpCompletionOption.ResponseHeadersRead).Result;

                using (var download = response.Content.ReadAsStreamAsync().Result)
                {
                    using (var output = new FileStream(downloadPath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        download.CopyTo(output);
                    }
                }
            }

            // We need to extract files to the tool file folder.  Note that when the
            // download includes multiple files, we'll extract all of them (ignoring
            // any we don't care about).

            if (userToolsFolder)
            {
                installFolder = ToolsFolder;
            }

            try
            {
                using (var download = File.OpenRead(downloadPath))
                {
                    using (var reader = ReaderFactory.Open(download))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            var entry = reader.Entry;

                            if (entry.IsDirectory)
                            {
                                continue;
                            }

                            using (var entryStream = reader.OpenEntryStream())
                            {
                                var lastSlashPos = entry.Key.LastIndexOfAny(new char[] { '/', '\\' });

                                Covenant.Assert(lastSlashPos >= 0);

                                var filename = entry.Key.Substring(lastSlashPos + 1);

                                // Ignore unnecessary files.

                                switch (filename)
                                {
                                    case "LICENSE":
                                    case "README.md":

                                        continue;
                                }

                                using (var output = File.OpenWrite(Path.Combine(installFolder, filename)))
                                {
                                    entryStream.CopyTo(output);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                NeonHelper.DeleteFile(downloadPath);
            }

            // We need to set execute permissions on Linux and OS/X.

            if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                NeonHelper.ExecuteCapture("chmod",
                    args: new object[]
                    {
                        "770",
                        toolPath
                    })
                    .EnsureSuccess();
            }

            return toolPath;
        }

        /// <summary>
        /// Returns the path to the a tool binary to be used by <b>neon-cli</b>.
        /// </summary>
        /// <param name="installFolder">Path to the tool installation folder.</param>
        /// <param name="userToolsFolder">
        /// Optionally specifies that instead of downloading missing tool binaries to <paramref name="installFolder"/>,
        /// the method will download the file to <see cref="ToolsFolder"/>.
        /// </param>
        /// <returns>The fully qualified tool path.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the tool cannot be located.</exception>
        /// <remarks>
        /// <para>
        /// If the <paramref name="installFolder"/> folder and the binary exist then we'll simply
        /// return the tool path when <paramref name="userToolsFolder"/><c>=true</c> and verify 
        /// that tool version is correct when <paramref name="userToolsFolder"/><c>=false</c>.
        /// </para>
        /// <para>
        /// If the <paramref name="installFolder"/> or binary does not exist, then the user is probably
        /// a developer running an uninstalled version of the tool, perhaps in the debugger.  In this case, 
        /// we're going to download the binaries to <paramref name="installFolder"/> by default or to 
        /// <see cref="ToolsFolder"/> when <paramref name="userToolsFolder"/><c>=true</c>.
        /// </para>
        /// </remarks>
        public static string GetHelmPath(string installFolder, bool userToolsFolder = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(installFolder), nameof(installFolder));

            Func<string, bool> toolChecker =
                toolPath =>
                {
                    // [helm version] output will look like:
                    //
                    //      version.BuildInfo{Version:"v3.3.1", GitCommit:"249e5215cde0c3fa72e27eb7a30e8d55c9696144", GitTreeState:"clean", GoVersion:"go1.14.7"}

                    try
                    {
                        var response      = NeonHelper.ExecuteCapture(toolPath, new object[] { "version" }).EnsureSuccess();
                        var versionOutput = response.OutputText;
                        var versionRegex  = new Regex(@"Version:""v(?'version'[\d.]+)""", RegexOptions.None);
                        var match         = versionRegex.Match(versionOutput);

                        if (match.Success)
                        {
                            return match.Groups["version"].Value == KubeVersions.Helm;
                        }
                        else
                        {
                            throw new Exception($"Unable to get [helm] version from: {versionOutput}");
                        }
                    }
                    catch
                    {
                        // [helm.exe] doesn't exist at that location or is invalid.

                        return false;
                    }
                };

            Func<string> toolUriRetriever =
                () =>
                {
                    if (NeonHelper.IsWindows)
                    {
                        return $"https://get.helm.sh/helm-v{KubeVersions.Helm}-windows-amd64.zip";
                    }
                    else if (NeonHelper.IsLinux)
                    {
                        return $"https://get.helm.sh/helm-v{KubeVersions.Helm}-linux-arm64.tar.gz";
                    }
                    else if (NeonHelper.IsOSX)
                    {
                        return $"https://get.helm.sh/helm-v{KubeVersions.Helm}-darwin-arm64.tar.gz";
                    }
                    else
                    {
                        throw new NotSupportedException(NeonHelper.OSDescription);
                    }
                };

            return GetToolPath(installFolder, "helm", toolChecker, toolUriRetriever, userToolsFolder);
        }

        /// <summary>
        /// Returns the credentials for a specific cluster user from the Glauth LDAP secret.
        /// </summary>
        /// <param name="k8s">The Kubernetes client.</param>
        /// <param name="username">The desired username.</param>
        /// <returns>The <see cref="GlauthUser"/> requested user credentials.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the user doesn't exist.</exception>
        public static async Task<GlauthUser> GetClusterLdapUserAsync(IKubernetes k8s, string username)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username), nameof(username));

            var users = await k8s.CoreV1.ReadNamespacedSecretAsync("glauth-users", KubeNamespace.NeonSystem);

            return NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(users.Data[username]));
        }

        /// <summary>
        /// Determines the health of a cluster by querying the API server.
        /// </summary>
        /// <param name="context">The cluster context.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>A <see cref="ClusterHealth"/> instance.</returns>
        public static async Task<ClusterHealth> GetClusterHealthAsync(KubeConfigContext context, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));

            // We're going to retrieve the special [neon-status/cluster-health] config map
            // and return the status from there.  This config map is created initially by
            // cluster setup and then is updated by neon-cluster-operator.

            var configFile = KubeHelper.KubeConfigPath;
            var config     = KubernetesClientConfiguration.BuildConfigFromConfigFile(configFile, currentContext: context.Name);

            if (config == null)
            {
                if (!config.SkipTlsVerify && config.SslCaCerts == null)
                {
                    var store = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser);

                    config.SslCaCerts = store.Certificates;
                }

                return new ClusterHealth()
                {
                    Version = "0",
                    State   = ClusterState.Unknown,
                    Summary = $"kubecontext for [{context.Name}] not found."
                };
            }
            
            using (var k8s = new k8s.Kubernetes(config, new KubernetesRetryHandler()))
            {
                // Cluster status is persisted to the [neon-status/cluster-health] configmap
                // during cluster setup and is maintained there after by [neon-cluster-operator].

                try
                {
                    return (await k8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterHealth>(KubeConfigMapName.ClusterHealth, KubeNamespace.NeonStatus)).Data;
                }
                catch (OperationCanceledException)
                {
                    return new ClusterHealth()
                    {
                        Version = "0",
                        State   = ClusterState.Unknown,
                        Summary = "Cluster health check cancelled"
                    };
                }
                catch (Exception e)
                {
                    return new ClusterHealth()
                    {
                        Version = "0",
                        State   = ClusterState.Unknown,
                        Summary = e.Message
                    };
                }
            }
        }

        /// <summary>
        /// Constructs an <b>initialized</b> Kubernetes object of a specific type.
        /// </summary>
        /// <typeparam name="T">The Kubernetes object type.</typeparam>
        /// <param name="name">Specifies the object name.</param>
        /// <returns>The new <typeparamref name="T"/>.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> does not define define string <b>KubeGroup</b>, 
        /// <b>KubeApiVersion</b> and <b>KubeKind</b> constants.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Unfortunately, the default constructors for objects like <see cref="V1ConfigMap"/> do not
        /// initialize the <see cref="IKubernetesObject.ApiVersion"/> and <see cref="IKubernetesObject.Kind"/>
        /// and properties even though these values will be the same for all instances of each object type.
        /// (I assume that Microsoft doesn't do this as an optimization that avoids initializing these
        /// properties and then doing that again when deserializing responses from the API server.
        /// </para>
        /// <para>
        /// This method constructs the request object and then configures its <see cref="IKubernetesObject.ApiVersion"/>
        /// and <see cref="IKubernetesObject.Kind"/> properties by reflecting <typeparamref name="T"/> and using
        /// the constant <b>KubeGroup</b>, <b>KubeApiVersion</b> and <b>KubeKind</b> values.  This is very convenient 
        /// but will be somwehat slower than setting these values explicitly but is probably worth the cost in most
        /// situations because Kubernetes objects are typically read much more often than being created.
        /// </para>
        /// <note>
        /// This method requires that <typeparamref name="T"/> define string <b>KubeGroup</b> <b>KubeApiVersion</b> 
        /// and <b>KubeKind</b> constants that return the correct values for the type.
        /// </note>
        /// </remarks>
        public static T CreateKubeObject<T>(string name)
            where T : IKubernetesObject, IMetadata<V1ObjectMeta>, new()
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var type            = typeof(T);
            var groupConst      = type.GetField("KubeGroup", BindingFlags.Public | BindingFlags.Static);
            var apiVersionConst = type.GetField("KubeApiVersion", BindingFlags.Public | BindingFlags.Static);
            var kindConst       = type.GetField("KubeKind", BindingFlags.Public | BindingFlags.Static);

            if (groupConst == null)
            {
                throw new NotSupportedException($"Object type [{type.FullName}] does not define the [KubeGroup] constant.");
            }

            var group = (string)groupConst.GetValue(null);

            if (apiVersionConst == null)
            {
                throw new NotSupportedException($"Object type [{type.FullName}] does not define the [KubeApiVersion] constant.");
            }

            var apiVersion = (string)apiVersionConst.GetValue(null);

            if (kindConst == null)
            {
                throw new NotSupportedException($"Object type [{type.FullName}] does not define the [KubeKind] constant.");
            }

            var kind = (string)kindConst.GetValue(null);
            var obj  = new T();

            obj.ApiVersion = String.IsNullOrEmpty(group) ? apiVersion : $"{group}/{apiVersion}";
            obj.Kind       = kind;
            obj.Metadata   = new V1ObjectMeta() { Name = name };

            return obj;
        }

        /// <summary>
        /// Determines whether a custom resource definition is a NEONKUBE custom resource.
        /// </summary>
        /// <param name="crd">The custom resource definition.</param>
        /// <returns><c>true</c> for NEONKUBE resource definitions.</returns>
        public static bool IsNeonKubeCustomResource(V1CustomResourceDefinition crd)
        {
            Covenant.Requires<ArgumentNullException>(crd != null, nameof(crd));

            return crd.Spec.Group.EndsWith($".{KubeConst.NeonKubeResourceGroup}");
        }

        /// <summary>
        /// Generates a unique(ish) pod name for application instances that are actually
        /// running outside of the cluster, typically for testing purposes.  This is based
        /// on the deployment name passed and a small UUID.
        /// </summary>
        /// <returns>The emulated pod name.</returns>
        public static string GetEmulatedPodName(string deployment)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(deployment), nameof(deployment));
            Covenant.Requires<ArgumentException>(ClusterDefinition.NameRegex.IsMatch(deployment), nameof(deployment));

            var uuid = NeonHelper.CreateBase36Uuid();

            return $"{deployment}-{uuid.Substring(0, 10)}-{uuid.Substring(uuid.Length - 5, 5)}".ToLowerInvariant();
        }

        /// <summary>
        /// Returns the tags to be included in all logs and root activity traces.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object>> TelemetryTags
        {
            get
            {
                if (cachedTelemetryTags != null)
                {
                    return cachedTelemetryTags;
                }

                cachedTelemetryTags = new List<KeyValuePair<string, object>>();

                cachedTelemetryTags.Add(new KeyValuePair<string, object>("client-id", ClientId));
                cachedTelemetryTags.Add(new KeyValuePair<string, object>("os", NeonHelper.OSDescription));
                cachedTelemetryTags.Add(new KeyValuePair<string, object>("cores", Environment.ProcessorCount));
                cachedTelemetryTags.Add(new KeyValuePair<string, object>("ram-mib", NeonHelper.MemoryMib));

                return cachedTelemetryTags;
            }
        }

        /// <summary>
        /// Gets the current namespace from within a pod.
        /// </summary>
        /// <returns>The current namespacee.</returns>
        public static async Task<string> GetCurrentNamespaceAsync()
        {
            if (NeonHelper.IsDevWorkstation)
            {
                return KubeNamespace.NeonSystem;
            }

            using (var reader = File.OpenText("/var/run/secrets/kubernetes.io/serviceaccount/namespace"))
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Returns the path to the <b>$/NEONKUBE/Lib/Neon.Kube/KubeVersions.cs</b> source file.
        /// </summary>
        /// <returns>The <b>KubeVersions.cd</b> path.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <b>NK_ROOT</b> environment variable does not exist or the git repo is
        /// invalid, the source file doesn't exist or the <see cref="KubeVersions.NeonKube"/>
        /// constant could not be located.
        /// </exception>
        private static string GetKubeVersionsPath()
        {
            var nkRoot = Environment.GetEnvironmentVariable("NK_ROOT");

            if (string.IsNullOrEmpty(nkRoot))
            {
                throw new InvalidOperationException("NK_ROOT environment variable does not exist.");
            }

            if (!Directory.Exists(nkRoot))
            {
                throw new InvalidOperationException($"[NK_ROOT={nkRoot}] directory does not exist.");
            }

            if (!File.Exists(Path.Combine(nkRoot, "neonKUBE.sln")))
            {
                throw new InvalidOperationException($"[NK_ROOT={nkRoot}] directory does not include the [neonKUBE.sln] file.");
            }

            var versionsPath = Path.Combine(nkRoot, "Lib", "Neon.Kube", "KubeVersions.cs");

            if (!File.Exists(versionsPath))
            {
                throw new InvalidOperationException($"[{versionsPath}] file does not exist.");
            }

            return versionsPath;
        }

        /// <summary>
        /// Returns the <see cref="KubeVersions.NeonKube"/> constant value extracted from the 
        /// <b>$/NEONKUBE/Lib/Neon.Kube/KubeVersions.cs</b> source file.  Note that the
        /// <b>NK_ROOT</b> environment variable must reference the root of the <b>NEONKUBE</b>
        /// git repository.
        /// </summary>
        /// <returns>The <b>NeonKube</b> version.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <b>NK_ROOT</b> environment variable does not exist or the git repo is
        /// invalid, the source file doesn't exist or the <see cref="KubeVersions.NeonKube"/>
        /// constant could not be located.
        /// </exception>
        public static SemanticVersion GetNeonKubeVersion()
        {
            var versionRegex = new Regex(@"^\s*public const string NeonKube = ""(?<version>.+)"";", RegexOptions.Multiline);
            var versionsPath = GetKubeVersionsPath();
            var versionsText = File.ReadAllText(versionsPath);
            var match        = versionRegex.Match(versionsText);

            if (!match.Success)
            {
                throw new InvalidOperationException($"[KubeVersions.NeonKube] constant not found.");
            }

            return SemanticVersion.Parse(match.Groups["version"].Value);
        }

        /// <summary>
        /// Edits the <b>$/NEONKUBE/Lib/Neon.Kube/KubeVersions.cs</b> source file by setting
        /// the <see cref="KubeVersions.NeonKube"/> constant to the version passed.
        /// </summary>
        /// <param name="version">The new version number.</param>
        /// <returns>
        /// <c>true</c> if the version constant value was changed, <c>false</c> when 
        /// the constant was already set to this version.
        /// </returns>
        public static bool EditNeonKubeVersion(SemanticVersion version)
        {
            Covenant.Requires<ArgumentNullException>(version != null, nameof(version));

            if (GetNeonKubeVersion() == version)
            {
                return false;
            }

            var versionsPath = GetKubeVersionsPath();
            var versionsText = File.ReadAllText(versionsPath);
            var versionRegex = new Regex(@"public\s+const\s+string\s+NeonKube\s+="".+""\s+;");
            var replaceText  = $"        public const string NeonKube = \"{version}\";";

            versionsText = versionRegex.Replace(versionsText, replaceText);

            File.WriteAllText(versionsPath, versionsText, Encoding.UTF8);

            return true;
        }

        /// <summary>
        /// Performs Open IC Connect Login
        /// </summary>
        /// <param name="authority">Specifies the authority.</param>
        /// <param name="clientId">Specifies the client ID.</param>
        /// <param name="scopes">Optionally specifies any scopes.</param>
        /// <returns>A <see cref="LoginResult"/>.</returns>
        public static async Task<LoginResult> LoginOidcAsync(
            string      authority,
            string      clientId,
            string[]    scopes = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(authority), nameof(authority));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clientId), nameof(clientId));

            var port     = NetHelper.GetUnusedTcpPort(KubePort.KubeFirstSsoPort, KubePort.KubeLastSsoPort, IPAddress.Loopback);
            var listener = new HttpListener();

            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            var options = 
                new OidcClientOptions
                {
                    Authority   = authority,
                    ClientId    = clientId,
                    RedirectUri = $"http://localhost:{port}",
                    Scope       = string.Join(" ", scopes),
                };

            var client = new OidcClient(options);

            // Generate start URL, state, nonce, code challenge.

            var state = await client.PrepareLoginAsync(new IdentityModel.Client.Parameters());

            NeonHelper.OpenBrowser($"{state.StartUrl}&code_verifier={state.CodeVerifier}");

            // Wait for the authorization response.

            var context        = await listener.GetContextAsync();

            var result = await client.ProcessResponseAsync($"?state={state.State}&code={context.Request.QueryString["code"]}", state);

            if (result.IsError)
            {
                throw new Exception(result.Error);
            }

            var responseString = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>Success</title>
</head>
<body style=""background-color: #979797;"">
<div style=""background-color: #c3c3c3; border-radius: 1em; margin: 1em; padding: 1em;"">
    <h1>Success!</h1>
    <p>You are now logged in. This window will close automatically in 5 seconds...</p>
</div>
<script>
    setTimeout(""window.close()"",5000) 
</script>
</body>
</html>";
            var buffer = Encoding.UTF8.GetBytes(responseString);

            context.Response.ContentLength64 = buffer.Length;

            using (var responseOutput = context.Response.OutputStream)
            {
                await responseOutput.WriteAsync(buffer, 0, buffer.Length);
            }

            context.Response.Close();

            listener.Stop();

            return result;
        }

        /// <summary>
        /// Creates a <see cref="IKubernetes"/> client from a kubeconfig file.
        /// </summary>
        /// <param name="kubeConfigPath">Optionally specifies the path to the kubecontext file.</param>
        /// <param name="currentContext">Optionally specifies the name of the context to use.</param>
        /// <returns>The <see cref="IKubernetes"/>.</returns>
        /// <remarks>
        /// This method just returns a client for the current context in the standard location when
        /// <paramref name="kubeConfigPath"/> isn't passed, otherwise it will load the kubeconfig
        /// from that path and return a client for the current context or the specific context identified
        /// by <paramref name="currentContext"/>.
        /// </remarks>
        public static IKubernetes CreateKubernetesClient(string kubeConfigPath = null, string currentContext = null)
        {
            KubernetesClientConfiguration   k8sConfig;

            if (kubeConfigPath != null)
            {
                k8sConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath: kubeConfigPath, currentContext: currentContext);
            }
            else
            {
                k8sConfig = KubernetesClientConfiguration.BuildDefaultConfig();
            }

            if (k8sConfig.SslCaCerts == null)
            {
                var store = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser);

                k8sConfig.SslCaCerts = store.Certificates;
            }

            return new k8s.Kubernetes(k8sConfig, new KubernetesRetryHandler());
        }

        /// <summary>
        /// Creates a <see cref="IKubernetes"/> client for the current cluster specified
        /// by a <see cref="Config.KubeConfig"/>.
        /// </summary>
        /// <param name="config">The source kubeconfig.</param>
        /// <returns>The <see cref="IKubernetes"/>.</returns>
        public static IKubernetes GetKubernetesClient(KubeConfig config)
        {
            Covenant.Requires<ArgumentNullException>(config != null, nameof(config));
            config.Validate(needsCurrentCluster: true);

            using (var tempFile = new TempFile(suffix: ".yaml"))
            {
                File.WriteAllText(tempFile.Path, NeonHelper.YamlSerialize(config));

                var k8sConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath: tempFile.Path);

                if (k8sConfig.SslCaCerts == null)
                {
                    var store = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser);

                    k8sConfig.SslCaCerts = store.Certificates;
                }

                return new k8s.Kubernetes(k8sConfig, new KubernetesRetryHandler());
            }
        }

        /// <summary>
        /// Verifies that the label name and valud conforms to the Kubernetes label constraints:
        /// https://kubernetes.io/docs/concepts/overview/working-with-objects/labels/#syntax-and-character-set
        /// </summary>
        /// <param name="labelType">
        /// Identifies the type of label being checked.  Any exceptions thrown wil;
        /// have their message text prefixed by this.
        /// </param>
        /// <param name="key">Specifies the label key.</param>
        /// <param name="value">Specifies the label value.</param>
        /// <exception cref="ClusterDefinitionException">Thrown when the label name or value invalid.</exception>
        public static void ValidateKubernetesLabel(string labelType, string key, string value)
        {
            Covenant.Requires<ArgumentNullException>(labelType != null, nameof(labelType));
            Covenant.Requires<ArgumentNullException>(key != null, nameof(key));
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));

            // Verify that custom node label name and value satisfies the following criteria:
            // 
            // NAMES:
            //
            //      1. Have an optional reverse domain prefix.
            //      2. Be at least one character long.
            //      3. Start and end with an alpha numeric character.
            //      4. Include only alpha numeric characters, dashes,
            //         underscores or dots.
            //      5. Does not have consecutive dots or dashes.
            //
            // VALUES:
            //
            //      1. Must start or end with an alphnumeric character.
            //      2. May include alphanumerics, dashes, underscores or dots
            //         between the beginning and ending characters.
            //      3. Values can be empty.
            //      4. Maximum length is 63 characters.

            if (key.Length == 0)
            {
                throw new ClusterDefinitionException($"{labelType}: Label key for value [{value}] is blank.");
            }

            var pSlash = key.IndexOf('/');
            var domain = pSlash == -1 ? null : key.Substring(0, pSlash);
            var name   = pSlash == -1 ? key : key.Substring(pSlash + 1);

            // Validate the NAME:

            if (domain != null)
            {
                if (!NetHelper.IsValidDnsHost(domain))
                {
                    throw new ClusterDefinitionException($"{labelType}: Label key [{key}] has an invalid reverse domain prefix.");
                }

                if (domain.Length > 253)
                {
                    throw new ClusterDefinitionException($"{labelType}: Label key [{key}] has a reverse domain prefix that's longer than 253 characters.");
                }
            }

            if (name.Length == 0)
            {
                throw new ClusterDefinitionException($"{labelType}: Label key [{key}] is empty.");
            }
            else if (name.Contains(".."))
            {
                throw new ClusterDefinitionException($"{labelType}: Label key [{key}] has consecutive dots.");
            }
            else if (name.Contains("--"))
            {
                throw new ClusterDefinitionException($"{labelType}: Label key [{key}] has consecutive dashes.");
            }
            else if (name.Contains("__"))
            {
                throw new ClusterDefinitionException($"{labelType}: Label key [{key}] has consecutive underscores.");
            }
            else if (!char.IsLetterOrDigit(name.First()))
            {
                throw new ClusterDefinitionException($"{labelType}: Label key [{key}] does not begin with a letter or digit.");
            }
            else if (!char.IsLetterOrDigit(name.Last()))
            {
                throw new ClusterDefinitionException($"{labelType}: Label key [{key}] does not end with a letter or digit.");
            }

            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_')
                {
                    continue;
                }

                throw new ClusterDefinitionException($"{labelType}: Label key [{key}] has an illegal character.  Only letters, digits, dashs, underscores and dots are allowed.");
            }

            // Validate the VALUE:

            if (value == string.Empty)
            {
                return;
            }

            if (!char.IsLetterOrDigit(value.First()) || !char.IsLetterOrDigit(value.First()))
            {
                throw new ClusterDefinitionException($"{labelType}: Label [{key}={value}] value is invalid.  Values must start and end with a letter or digit.");
            }

            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_')
                {
                    continue;
                }

                throw new ClusterDefinitionException($"{labelType}: Label value [{key}={value}] has an illegal character.  Only letters, digits, dashs, underscores and dots are allowed.");
            }

            if (value.Length > 63)
            {
                throw new ClusterDefinitionException($"{labelType}: Label value [{key}={value}] is too long.  Values can have a maximum of 63 characters.");
            }
        }

        /// <summary>
        /// Locates the most recent <b>neon-cli</b> executable.  Note that this returns the path
        /// to our version of <b>kubectl</b> rather than the <b>neon-cli</b> executable that implements
        /// our customized subcommands.
        /// </summary>
        /// <returns>The path to the executable.</returns>
        /// <exception cref="FileNotFoundException">Thrown when no executable was found.</exception>
        /// <remarks>
        /// <para>
        /// This method is intended to work well on normal user machines where the neonKUBE/neonCLOUD
        /// sources and build environments are not configured as well as for maintainers that need to
        /// execute most recent executable to test/debug recent changes.
        /// </para>
        /// <para>
        /// If either of <b>neon-cli</b> or <b>neon-desktop</b> is installed on the workstation
        /// (determined by the presence of the <b>NEON_INSTALL_FOLDER</b> environment variable),
        /// we'll return the path to the executable from the installation folder.  When those are
        /// not installed, we'll return <b>$(NC_ROOT)/Build/neon-cli/neon.exe</b>.
        /// </para>
        /// <para>
        /// A <see cref="FileNotFoundException"/> will be thrown we couldn't locate the executable.
        /// </para>
        /// </remarks>
        public static string NeonCliPath
        {
            get
            {
                if (!string.IsNullOrEmpty(cachedNeonCliPath))
                {
                    return cachedNeonCliPath;
                }

                var neonInstallFolder = Environment.GetEnvironmentVariable("NEON_INSTALL_FOLDER");
                var ncRoot            = Environment.GetEnvironmentVariable("NC_ROOT");
                var neonPath          = (string)null;

                if (!string.IsNullOrEmpty(neonInstallFolder))
                {
                    neonPath = Path.Combine(neonInstallFolder, "neon.exe");
                }

                if (!string.IsNullOrEmpty(ncRoot) && (neonPath == null || !File.Exists(neonPath)))
                {
                    neonPath = Path.Combine(ncRoot, "Build", "neon-cli", "neon.exe");
                }

                if (neonPath == null || !File.Exists(neonPath))
                {
                    string details;

                    if (!string.IsNullOrEmpty(neonInstallFolder))
                    {
                        details = "The [neon.exe] program could not be located within the installation folder.\r\n\r\nYou may need to reinstall from here: https://github.com/nforgeio/neonKUBE/releases";
                    }
                    else if (!string.IsNullOrEmpty(ncRoot))
                    {
                        details = "MAINTAINERS: Rebuild the executable via:\r\n\r\nneoncloud-builder -dirty -noclean -nobuild -kubectlonly";
                    }
                    else
                    {
                        details = "You'll need to install one of [neon-cli] or [neon-desktop] from here:\r\n\r\nhttps://github.com/nforgeio/neonKUBE/releases";
                    }

                    throw new FileNotFoundException($"[{neonPath}] does not exist.\r\n\r\n{details}");
                }

                return cachedNeonCliPath = neonPath;
            }
        }

        /// <summary>
        /// Builds the <b>neon/kubectl</b> tool if it does not already exist.
        /// </summary>
        private static void EnsureNeonKubectl()
        {
            try
            {
                _ = NeonCliPath;
            }
            catch (FileNotFoundException)
            {
                var response = NeonHelper.ExecuteCapture("neoncloud-builder",
                    new object[]
                    {
                        "-dirty",
                        "-noclean",
                        "-nobuild",
                        "-kubectlonly"
                    });

                response.EnsureSuccess();
            }
        }

        /// <summary>
        /// Executes a <b>neon/kubectl</b> command using the installed executable or the
        /// executable from the NEONCLOUD build folder.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The command exit code.</returns>
        /// <remarks>
        /// <note>
        /// For maintainers, this method will build the <b>neon/kubectl</b> tool if it does not already exist.
        /// </note>
        /// </remarks>
        public static async Task<int> NeonCliExecuteAsync(object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            EnsureNeonKubectl();

            return await NeonHelper.ExecuteAsync(NeonCliPath, args);
        }

        /// <summary>
        /// Executes a <b>neon/kubectl</b> command using the installed executable or the
        /// executable from the NEONCLOUD build folder, capturing the output streams.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The command exit code.</returns>
        /// <remarks>
        /// <note>
        /// For maintainers, this method will build the <b>neon/kubectl</b> tool if it does not already exist.
        /// </note>
        /// </remarks>
        public static async Task<ExecuteResponse> NeonCliExecuteCaptureAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            EnsureNeonKubectl();

            return await NeonHelper.ExecuteCaptureAsync(NeonCliPath, args);
        }

        /// <summary>
        /// Executes a <b>neon/kubectl</b> command using the installed executable or the
        /// executable from the NEONCLOUD build folder, capturing the output streams.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The command exit code.</returns>
        /// <remarks>
        /// <note>
        /// For maintainers, this method will build the <b>neon/kubectl</b> tool if it does not already exist.
        /// </note>
        /// </remarks>
        public static ExecuteResponse NeonCliExecuteCapture(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            EnsureNeonKubectl();

            return NeonHelper.ExecuteCapture(NeonCliPath, args);
        }
    }
}
