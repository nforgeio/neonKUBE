//-----------------------------------------------------------------------------
// FILE:	    KubeHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;

using Couchbase;
using Newtonsoft.Json;

using k8s;
using k8s.Models;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Windows;
using Neon.Cryptography;

namespace Neon.Kube
{
    /// <summary>
    /// cluster related utilties.
    /// </summary>
    public static partial class KubeHelper
    {
        private static INeonLogger          log = LogManager.Default.GetLogger(typeof(KubeHelper));
        private static string               orgKUBECONFIG;
        private static string               testFolder;
        private static DesktopClient        desktopClient;
        private static KubeConfig           cachedConfig;
        private static KubeConfigContext    cachedContext;
        private static HeadendClient        cachedHeadendClient;
        private static string               cachedNeonKubeUserFolder;
        private static string               cachedKubeUserFolder;
        private static string               cachedRunFolder;
        private static string               cachedLogFolder;
        private static string               cachedTempFolder;
        private static string               cachedLoginsFolder;
        private static string               cachedPasswordsFolder;
        private static string               cachedCacheFolder;
        private static string               cachedDesktopFolder;
        private static KubeClientConfig     cachedClientConfig;
        private static X509Certificate2     cachedClusterCertificate;
        private static string               cachedProgramFolder;
        private static string               cachedPwshPath;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static KubeHelper()
        {
            // Check if we need to run in test mode.

            var folder = Environment.GetEnvironmentVariable(NeonHelper.TestModeFolderVar);

            if (!string.IsNullOrEmpty(folder))
            {
                // Yep: this is test mode.

                testFolder = folder;
            }
        }

        /// <summary>
        /// Clears all cached items.
        /// </summary>
        private static void ClearCachedItems()
        {
            cachedConfig             = null;
            cachedContext            = null;
            cachedHeadendClient      = null;
            cachedNeonKubeUserFolder = null;
            cachedKubeUserFolder     = null;
            cachedRunFolder          = null;
            cachedLogFolder          = null;
            cachedTempFolder         = null;
            cachedLoginsFolder     = null;
            cachedPasswordsFolder    = null;
            cachedCacheFolder        = null;
            cachedDesktopFolder      = null;
            cachedClientConfig       = null;
            cachedClusterCertificate = null;
            cachedProgramFolder      = null;
            cachedPwshPath           = null;
        }

        /// <summary>
        /// Explicitly sets the class <see cref="INeonLogger"/> implementation.  This defaults to
        /// a reasonable value.
        /// </summary>
        /// <param name="log"></param>
        public static void SetLogger(INeonLogger log)
        {
            Covenant.Requires<ArgumentNullException>(log != null, nameof(log));

            KubeHelper.log = log;
        }

        /// <summary>
        /// Puts <see cref="KubeHelper"/> into test mode to support unit testing.  This
        /// changes the folders where Kubernetes and neonKUBE persists their state to
        /// directories beneath the folder passed.  This also modifies the KUBECONFIG
        /// environment variable to reference the new location.
        /// </summary>
        /// <param name="folder">Specifies the folder where the state will be persisted.</param>
        public static void SetTestMode(string folder)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(folder), nameof(folder));

            if (IsTestMode)
            {
                throw new InvalidOperationException("Already running in test mode.");
            }

            if (!Directory.Exists(folder))
            {
                throw new FileNotFoundException($"Folder [{folder}] does not exist.");
            }

            ClearCachedItems();

            testFolder    = folder;
            orgKUBECONFIG = Environment.GetEnvironmentVariable("KUBECONFIG");

            Environment.SetEnvironmentVariable("KUBECONFIG", Path.Combine(testFolder, ".kube", "config"));
        }

        /// <summary>
        /// Resets the test mode, restoring normal operation.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if a parent process set test mode.</exception>
        public static void ResetTestMode()
        {
            if (string.IsNullOrEmpty(orgKUBECONFIG))
            {
                throw new InvalidOperationException("Cannot reset test mode because that was set by a parent process.");
            }

            ClearCachedItems();
            testFolder = null;
        }

        /// <summary>
        /// Returns <c>true</c> if the class is running in test mode.
        /// </summary>
        public static bool IsTestMode => testFolder != null;

        /// <summary>
        /// Returns the <see cref="DesktopClient"/> suitable for communicating
        /// with the neonDESKTOP application.
        /// </summary>
        public static DesktopClient Desktop
        {
            get
            {
                if (desktopClient == null)
                {
                    desktopClient = new DesktopClient($"http://localhost:{ClientConfig.DesktopServicePort}/");
                }

                return desktopClient;
            }
        }

        /// <summary>
        /// Reads a file as text, retrying if the file is already open.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The file text.</returns>
        /// <remarks>
        /// It's possible for the configuration file to be temporarily opened
        /// by another process (e.g. the neonDESKTOP application or a 
        /// command line tool).  Rather than throw an exception, we're going
        /// to retry the operation a few times.
        /// </remarks>
        internal static string ReadFileTextWithRetry(string path)
        {
            var retry = new LinearRetryPolicy(typeof(IOException), maxAttempts: 10, retryInterval: TimeSpan.FromMilliseconds(200));
            var text = string.Empty;

            retry.InvokeAsync(
                async () => 
                {
                    await Task.CompletedTask;

                    text = File.ReadAllText(path);

                }).Wait();

            return text;
        }

        /// <summary>
        /// Writes a file as text, retrying if the file is already open.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="text">The text to be written.</param>
        /// <remarks>
        /// It's possible for the configuration file to be temporarily opened
        /// by another process (e.g. the neonDESKTOP application or a 
        /// command line tool).  Rather than throw an exception, we're going
        /// to retry the operation a few times.
        /// </remarks>
        internal static string WriteFileTextWithRetry(string path, string text)
        {
            var retry = new LinearRetryPolicy(typeof(IOException), maxAttempts: 10, retryInterval: TimeSpan.FromMilliseconds(200));

            retry.InvokeAsync(
                async () =>
                {
                    await Task.CompletedTask;

                    File.WriteAllText(path, text);

                }).Wait();

            return text;
        }

        /// <summary>
        /// Accesses the neonDESKTOP client configuration.
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
                    cachedClientConfig = NeonHelper.JsonDeserialize<KubeClientConfig>(ReadFileTextWithRetry(clientStatePath));
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
                Covenant.Requires<ArgumentNullException>(value != null, nameof(value));

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
        /// Encrypts a file or directory when supported by the underlying operating system
        /// and file system.  Currently, this only works on non-HOME versions of Windows
        /// and NTFS file systems.  This fails silently.
        /// </summary>
        /// <param name="path">The file or directory path.</param>
        /// <returns><c>true</c> if the operation was successful.</returns>
        private static bool EncryptFile(string path)
        {
            try
            {
                return Win32.EncryptFile(path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures that sensitive folders and files on the local workstation are encrypted at rest
        /// for security purposes.  These include the users <b>.kube</b>, <b>.neonkube</b>, and any
        /// the <b>OpenVPN</b> if it exists.
        /// </summary>
        public static void EncryptSensitiveFiles()
        {
            if (NeonHelper.IsWindows)
            {
                var userFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                var sensitiveFolders = new string[]
                {
                    Path.Combine(userFolderPath, ".kube"),
                    Path.Combine(userFolderPath, ".neonkube"),
                    Path.Combine(userFolderPath, "OpenVPN")
                };

                foreach (var sensitiveFolder in sensitiveFolders)
                {
                    if (Directory.Exists(sensitiveFolder))
                    {
                        KubeHelper.EncryptFile(sensitiveFolder);
                    }
                }
            }
            else
            {
                // $todo(jefflill): Implement this for OS/X

                // throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current application is running in the special 
        /// <b>neon-cli</b> container as a shimmed application.
        /// </summary>
        public static bool InToolContainer
        {
            get { return Environment.GetEnvironmentVariable("NEON_TOOL_CONTAINER") == "1"; }
        }

        /// <summary>
        /// Returns the <see cref="KubeHostPlatform"/> for the current workstation.
        /// </summary>
        public static KubeHostPlatform HostPlatform
        {
            get
            {
                if (NeonHelper.IsLinux)
                {
                    return KubeHostPlatform.Linux;
                }
                else if (NeonHelper.IsOSX)
                {
                    return KubeHostPlatform.Osx;
                }
                else if (NeonHelper.IsWindows)
                {
                    return KubeHostPlatform.Windows;
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
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for cloud environments.</returns>
        public static bool IsCloudEnvironment(HostingEnvironment hostingEnvironment)
        {
            switch (hostingEnvironment)
            {
                // On-premise environments

                case HostingEnvironment.BareMetal:
                case HostingEnvironment.HyperV:
                case HostingEnvironment.HyperVLocal:
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
        /// Determines whether a cluster hosting environment deploys on-premise.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for on-premise environments.</returns>
        public static bool IsOnPremiseEnvironment(HostingEnvironment hostingEnvironment)
        {
            return !IsCloudEnvironment(hostingEnvironment);
        }

        /// <summary>
        /// Returns a <see cref="HeadendClient"/>.
        /// </summary>
        public static HeadendClient Headend
        {
            get
            {
                if (cachedHeadendClient != null)
                {
                    return cachedHeadendClient;
                }

                return cachedHeadendClient = new HeadendClient();
            }
        }

        /// <summary>
        /// Returns the path the folder holding the user specific Kubernetes files.
        /// </summary>
        /// <param name="ignoreNeonToolContainerVar">
        /// Optionally ignore the presence of a <b>NEON_TOOL_CONTAINER</b> environment 
        /// variable.  Defaults to <c>false</c>.
        /// </param>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// The actual path return depends on the presence of the <b>NEON_TOOL_CONTAINER</b>
        /// environment variable.  <b>NEON_TOOL_CONTAINER=1</b> then we're running in a 
        /// shimmed Docker container and we'll expect the cluster login information to be mounted
        /// at <b>/neonkube</b>.  Otherwise, we'll return a suitable path within the 
        /// current user's home directory.
        /// </remarks>
        public static string GetNeonKubeUserFolder(bool ignoreNeonToolContainerVar = false)
        {
            if (!ignoreNeonToolContainerVar && InToolContainer)
            {
                return "/neonkube";
            }

            if (cachedNeonKubeUserFolder != null)
            {
                return cachedNeonKubeUserFolder;
            }

            if (IsTestMode)
            {
                cachedNeonKubeUserFolder = Path.Combine(testFolder, ".neonkube");

                Directory.CreateDirectory(cachedNeonKubeUserFolder);

                return cachedNeonKubeUserFolder;
            }

            if (NeonHelper.IsWindows)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".neonkube");

                Directory.CreateDirectory(path);

                try
                {
                    EncryptFile(path);
                }
                catch
                {
                    // Encryption is not available on all platforms (e.g. Windows Home, or non-NTFS
                    // file systems).  The secrets won't be encrypted for these situations.
                }

                return cachedNeonKubeUserFolder = path;
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".neonkube");

                Directory.CreateDirectory(path);

                return cachedNeonKubeUserFolder = path;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the path the folder holding the user specific Kubernetes configuration files.
        /// </summary>
        /// <param name="ignoreNeonToolContainerVar">
        /// Optionally ignore the presence of a <b>NEON_TOOL_CONTAINER</b> environment 
        /// variable.  Defaults to <c>false</c>.
        /// </param>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// The actual path return depends on the presence of the <b>NEON_TOOL_CONTAINER</b>
        /// environment variable.  <b>NEON_TOOL_CONTAINER=1</b> then we're running in a 
        /// shimmed Docker container and we'll expect the cluster login information to be mounted
        /// at <b>/$HOME/.kube</b>.  Otherwise, we'll return a suitable path within the 
        /// current user's home directory.
        /// </remarks>
        public static string GetKubeUserFolder(bool ignoreNeonToolContainerVar = false)
        {
            if (!ignoreNeonToolContainerVar && InToolContainer)
            {
                return $"/{Environment.GetEnvironmentVariable("HOME")}/.kube";
            }

            if (cachedKubeUserFolder != null)
            {
                return cachedKubeUserFolder;
            }

            if (IsTestMode)
            {
                cachedKubeUserFolder = Path.Combine(testFolder, ".kube");

                Directory.CreateDirectory(cachedKubeUserFolder);

                return cachedKubeUserFolder;
            }

            if (NeonHelper.IsWindows)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".kube");

                Directory.CreateDirectory(path);

                try
                {
                    EncryptFile(path);
                }
                catch
                {
                    // Encryption is not available on all platforms (e.g. Windows Home, or non-NTFS
                    // file systems).  The secrets won't be encrypted for these situations.
                }

                return cachedKubeUserFolder = path;
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".kube");

                Directory.CreateDirectory(path);

                return cachedKubeUserFolder = path;
            }
            else
            {
                throw new NotImplementedException();
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

                var path = Path.Combine(GetNeonKubeUserFolder(), "run");

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

                var path = Path.Combine(GetNeonKubeUserFolder(), "log");

                Directory.CreateDirectory(path);

                return cachedLogFolder = path;
            }
        }

        /// <summary>
        /// Returns the path the neonFORGE temporary folder, creating the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage clusters.  The client will use this to store temporary files that may
        /// include sensitive information because these folders are encrypted on disk.
        /// </remarks>
        public static string TempFolder
        {
            get
            {
                if (cachedTempFolder != null)
                {
                    return cachedTempFolder;
                }

                var path = Path.Combine(GetNeonKubeUserFolder(), "temp");

                Directory.CreateDirectory(path);

                return cachedTempFolder = path;
            }
        }

        /// <summary>
        /// Returns the path to the Kubernetes configuration file.
        /// </summary>
        public static string KubeConfigPath => Path.Combine(KubeHelper.GetKubeUserFolder(), "config");

        /// <summary>
        /// Returns the path the folder containing cluster login files, creating the folder 
        /// if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// <para>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage clusters.  Each known cluster will have a JSON file named
        /// <b><i>NAME</i>.context.json</b> holding the serialized <see cref="ClusterLogin"/> 
        /// information for the cluster, where <i>NAME</i> maps to a cluster configuration name
        /// within the <c>kubeconfig</c> file.
        /// </para>
        /// </remarks>
        public static string LoginsFolder
        {
            get
            {
                if (cachedLoginsFolder != null)
                {
                    return cachedLoginsFolder;
                }

                var path = Path.Combine(GetNeonKubeUserFolder(), "logins");

                Directory.CreateDirectory(path);

                return cachedLoginsFolder = path;
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

                var path = Path.Combine(GetNeonKubeUserFolder(), "passwords");

                Directory.CreateDirectory(path);

                return cachedPasswordsFolder = path;
            }
        }

        /// <summary>
        /// Returns path to the neonDESKTOP application state folder.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string DesktopFolder
        {
            get
            {
                if (cachedDesktopFolder != null)
                {
                    return cachedDesktopFolder;
                }

                var path = Path.Combine(GetNeonKubeUserFolder(), "desktop");

                Directory.CreateDirectory(path);

                return cachedDesktopFolder = path;
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

                var path = Path.Combine(GetNeonKubeUserFolder(), "cache");

                Directory.CreateDirectory(path);

                return cachedCacheFolder = path;
            }
        }

        /// <summary>
        /// Returns the path to the folder containing cached files for the specified platform.
        /// </summary>
        /// <param name="platform">Identifies the platform.</param>
        /// <returns>The folder path.</returns>
        public static string GetPlatformCacheFolder(KubeHostPlatform platform)
        {
            string subfolder;

            switch (platform)
            {
                case KubeHostPlatform.Linux:

                    subfolder = "linux";
                    break;

                case KubeHostPlatform.Osx:

                    subfolder = "osx";
                    break;

                case KubeHostPlatform.Windows:

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
        /// Returns the path to the cached file for a specific named component with optional version.
        /// </summary>
        /// <param name="platform">Identifies the platform.</param>
        /// <param name="component">The component name.</param>
        /// <param name="version">The component version (or <c>null</c>).</param>
        /// <returns>The component file path.</returns>
        public static string GetCachedComponentPath(KubeHostPlatform platform, string component, string version)
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
        /// Returns the path to the cluster login file path for a specific context
        /// by raw name.
        /// </summary>
        /// <param name="contextName">The kubecontext name.</param>
        /// <returns>The file path.</returns>
        public static string GetClusterLoginPath(KubeContextName contextName)
        {
            Covenant.Requires<ArgumentNullException>(contextName != null, nameof(contextName));

            // Kubecontext names may include a forward slash to specify a Kubernetes
            // namespace.  This won't work for a file name, so we're going to replace
            // any of these with a "~".

            var rawName = (string)contextName;

            return Path.Combine(LoginsFolder, $"{rawName.Replace("/", "~")}.login.yaml");
        }

        /// <summary>
        /// Returns the cluster login for the structured configuration name.
        /// </summary>
        /// <param name="name">The structured context name.</param>
        /// <returns>The <see cref="ClusterLogin"/> or <c>null</c>.</returns>
        public static ClusterLogin GetClusterLogin(KubeContextName name)
        {
            Covenant.Requires<ArgumentNullException>(name != null, nameof(name));

            var path = GetClusterLoginPath(name);

            if (!File.Exists(path))
            {
                return null;
            }

            var extension = NeonHelper.YamlDeserialize<ClusterLogin>(ReadFileTextWithRetry(path));

            extension.SetPath(path);
            extension.ClusterDefinition?.Validate();

            return extension;
        }

        /// <summary>
        /// Returns the path to the current user's cluster virtual machine templates
        /// folder, creating the directory if it doesn't already exist.
        /// </summary>
        /// <returns>The path to the cluster setup folder.</returns>
        public static string VmTemplatesFolder
        {
            get
            {
                var path = Path.Combine(GetNeonKubeUserFolder(), "vm-templates");

                Directory.CreateDirectory(path);

                return path;
            }
        }

        /// <summary>
        /// Returns the path to the neonKUBE program folder.
        /// </summary>
        public static string ProgramFolder
        {
            get
            {
                if (cachedProgramFolder != null)
                {
                    return cachedProgramFolder;
                }

                cachedProgramFolder = Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER");

                if (cachedProgramFolder == null)
                {
                    cachedProgramFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "neonKUBE");

                    // For some reason, [SpecialFolder.ProgramFiles] is returning: 
                    //
                    //      C:\Program Files (x86)
                    //
                    // We're going to strip off the " (x86)" part if present.

                    cachedProgramFolder = cachedProgramFolder.Replace(" (x86)", string.Empty);
                }

                if (!Directory.Exists(cachedProgramFolder))
                {
                    Directory.CreateDirectory(cachedProgramFolder);
                }

                return cachedProgramFolder;
            }
        }

        /// <summary>
        /// Returns the path to the Powershell Core executable to be used.
        /// This will first examine the <b>NEONDESKTOP_PROGRAM_FOLDER</b> environment
        /// variable to see if the installed version of Powershell Core should
        /// be used, otherwise it will simply return <b>pwsh.exe</b> so that
        /// the <b>PATH</b> will be searched.
        /// </summary>
        public static string PwshPath
        {
            get
            {
                if (cachedPwshPath != null)
                {
                    return cachedPwshPath;
                }

                if (!string.IsNullOrEmpty(ProgramFolder))
                {
                    var pwshPath = Path.Combine(ProgramFolder, "powershell", "pwsh.exe");

                    if (File.Exists(pwshPath))
                    {
                        return cachedPwshPath = pwshPath;
                    }
                }

                return cachedPwshPath = "pwsh.exe";
            }
        }

        /// <summary>
        /// Loads or reloads the Kubernetes configuration.
        /// </summary>
        /// <returns>The <see cref="Config"/>.</returns>
        public static KubeConfig LoadConfig()
        {
            cachedConfig = null;
            return Config;
        }

        /// <summary>
        /// Returns the user's current <see cref="Config"/>.
        /// </summary>
        public static KubeConfig Config
        {
            get
            {
                if (cachedConfig != null)
                {
                    return cachedConfig;
                }

                var configPath = KubeConfigPath;

                if (File.Exists(configPath))
                {
                    return cachedConfig = NeonHelper.YamlDeserialize<KubeConfig>(ReadFileTextWithRetry(configPath));
                }

                return cachedConfig = new KubeConfig();
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

            WriteFileTextWithRetry(KubeConfigPath, NeonHelper.YamlSerialize(config));
        }

        /// <summary>
        /// This is used for special situations for setting up a cluster to
        /// set an uninitialized Kubernetes config context as the current
        /// <see cref="CurrentContext"/>.
        /// </summary>
        /// <param name="context">The context being set or <c>null</c> to reset.</param>
        public static void InitContext(KubeConfigContext context = null)
        {
            cachedContext = context;
        }

        /// <summary>
        /// Sets the current Kubernetes config context.
        /// </summary>
        /// <param name="contextName">The context name of <c>null</c> to clear the current context.</param>
        /// <exception cref="ArgumentException">Thrown if the context specified doesnt exist.</exception>
        public static void SetCurrentContext(KubeContextName contextName)
        {
            if (contextName == null)
            {
                cachedContext         = null;
                Config.CurrentContext = null;
            }
            else
            {
                var newContext = Config.GetContext(contextName);

                if (newContext == null)
                {
                    throw new ArgumentException($"Kubernetes [context={contextName}] does not exist.", nameof(contextName));
                }

                cachedContext         = newContext;
                Config.CurrentContext = (string)contextName;
            }

            cachedClusterCertificate = null;

            Config.Save();
        }

        /// <summary>
        /// Sets the current Kubernetes config context by string name.
        /// </summary>
        /// <param name="contextName">The context name of <c>null</c> to clear the current context.</param>
        /// <exception cref="ArgumentException">Thrown if the context specified doesnt exist.</exception>
        public static void SetCurrentContext(string contextName)
        {
            SetCurrentContext((KubeContextName)contextName);
        }

        /// <summary>
        /// Returns the <see cref="CurrentContext"/> for the connected cluster
        /// or <c>null</c> when there is no current context.
        /// </summary>
        public static KubeConfigContext CurrentContext
        {
            get
            {
                if (cachedContext != null)
                {
                    return cachedContext;
                }

                if (string.IsNullOrEmpty(Config.CurrentContext))
                {
                    return null;
                }
                else
                {
                    return Config.GetContext(Config.CurrentContext);
                }
            }
        }

        /// <summary>
        /// Returns the current context's <see cref="CurrentContextName"/> or <c>null</c>
        /// if there's no current context.
        /// </summary>
        public static KubeContextName CurrentContextName => CurrentContext == null ? null : KubeContextName.Parse(CurrentContext.Name);

        /// <summary>
        /// Returns the Kuberneties API service certificate for the current
        /// cluster context or <c>null</c> if we're not connected to a cluster.
        /// </summary>
        public static X509Certificate2 ClusterCertificate
        {
            get
            {
                if (cachedClusterCertificate != null)
                {
                    return cachedClusterCertificate;
                }

                if (CurrentContext == null)
                {
                    return null;
                }

                var cluster = KubeHelper.Config.GetCluster(KubeHelper.CurrentContext.Properties.Cluster);
                var certPem = Encoding.UTF8.GetString(Convert.FromBase64String(cluster.Properties.CertificateAuthorityData));
                var tlsCert = TlsCertificate.FromPemParts(certPem);

                return cachedClusterCertificate = tlsCert.ToX509();
            }
        }

        /// <summary>
        /// Returns the Kuberneties API client certificate for the current
        /// cluster context or <c>null</c> if we're not connected to a cluster.
        /// </summary>
        public static X509Certificate2 ClientCertificate
        {
            get
            {
                if (cachedClusterCertificate != null)
                {
                    return cachedClusterCertificate;
                }

                if (CurrentContext == null)
                {
                    return null;
                }

                var userContext = KubeHelper.Config.GetUser(KubeHelper.CurrentContext.Properties.User);
                var certPem     = Encoding.UTF8.GetString(Convert.FromBase64String(userContext.Properties.ClientCertificateData));
                var keyPem      = Encoding.UTF8.GetString(Convert.FromBase64String(userContext.Properties.ClientKeyData));
                var tlsCert     = TlsCertificate.FromPemParts(certPem, keyPem);
                var clientCert  = tlsCert.ToX509();

                return null;
            }
        }

        /// <summary>
        /// Looks for a certificate with a friendly name.
        /// </summary>
        /// <param name="store">The certificate store.</param>
        /// <param name="friendlyName">The case insensitive friendly name.</param>
        /// <returns>The certificate or <c>null</c> if one doesn't exist by the name.</returns>
        private static X509Certificate2 FindCertificateByFriendlyName(X509Store store, string friendlyName)
        {
            Covenant.Requires<ArgumentNullException>(store != null, nameof(store));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(friendlyName), nameof(friendlyName));

            foreach (var certificate in store.Certificates)
            {
                if (friendlyName.Equals(certificate.FriendlyName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return certificate;
                }
            }

            return null;
        }

        /// <summary>
        /// <para>
        /// Ensures that <b>kubectl</b> tool whose version is at least as great as the Kubernetes
        /// cluster version is installed to the <b>neonKUBE</b> programs folder by copying the
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
        /// <param name="setupInfo">The KUbernetes setup information.</param>
        public static void InstallKubeCtl(KubeSetupInfo setupInfo)
        {
            Covenant.Requires<ArgumentNullException>(setupInfo != null, nameof(setupInfo));

            var hostPlatform      = KubeHelper.HostPlatform;
            var cachedKubeCtlPath = KubeHelper.GetCachedComponentPath(hostPlatform, "kubectl", KubeVersions.KubernetesVersion);
            var targetPath        = Path.Combine(KubeHelper.ProgramFolder);

            switch (hostPlatform)
            {
                case KubeHostPlatform.Windows:

                    targetPath = Path.Combine(targetPath, "kubectl.exe");

                    // Ensure that the KUBECONFIG environment variable exists and includes
                    // the path to the user's [.neonkube] configuration.

                    var kubeConfigVar = Environment.GetEnvironmentVariable("KUBECONFIG");

                    if (string.IsNullOrEmpty(kubeConfigVar))
                    {
                        // The [KUBECONFIG] environment variable doesn't exist so we'll set it.

                        Registry.SetValue(@"HKEY_CURRENT_USER\Environment", "KUBECONFIG", KubeConfigPath, RegistryValueKind.ExpandString);
                        Environment.SetEnvironmentVariable("KUBECONFIG", KubeConfigPath);
                    }
                    else
                    {
                        // The [KUBECONFIG] environment variable exists but we still need to
                        // ensure that the path to our [USER/.neonkube] config is present.

                        var sb    = new StringBuilder();
                        var found = false;

                        foreach (var path in kubeConfigVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (path == KubeConfigPath)
                            {
                                found = true;
                            }

                            sb.AppendWithSeparator(path, ";");
                        }

                        if (!found)
                        {
                            sb.AppendWithSeparator(KubeConfigPath, ";");
                        }

                        var newKubeConfigVar = sb.ToString();

                        if (newKubeConfigVar != kubeConfigVar)
                        {
                            Registry.SetValue(@"HKEY_CURRENT_USER\Environment", "KUBECONFIG", newKubeConfigVar, RegistryValueKind.ExpandString);
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
                            throw new KubeException(error);
                        }

                        pStart += pattern.Length;

                        var pEnd = response.OutputText.IndexOf("\"", pStart);

                        if (pEnd == -1)
                        {
                            throw new KubeException(error);
                        }

                        var currentVersionString = response.OutputText.Substring(pStart, pEnd - pStart);

                        if (!Version.TryParse(currentVersionString, out var currentVersion))
                        {
                            throw new KubeException(error);
                        }

                        if (Version.Parse(KubeVersions.KubernetesVersion) > currentVersion)
                        {
                            // We need to copy the latest version.

                            if (File.Exists(targetPath))
                            {
                                File.Delete(targetPath);
                            }

                            File.Copy(cachedKubeCtlPath, targetPath);
                        }
                    }
                    break;

                case KubeHostPlatform.Linux:
                case KubeHostPlatform.Osx:
                default:

                    throw new NotImplementedException($"[{hostPlatform}] support is not implemented.");
            }
        }

        /// <summary>
        /// <para>
        /// Ensures that <b>helm</b> tool whose version is at least as great as the requested
        /// cluster version is installed to the <b>neonKUBE</b> programs folder by copying the
        /// tool from the cache if necessary.
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
        /// <param name="setupInfo">The KUbernetes setup information.</param>
        public static void InstallHelm(KubeSetupInfo setupInfo)
        {
            Covenant.Requires<ArgumentNullException>(setupInfo != null, nameof(setupInfo));

            var hostPlatform    = KubeHelper.HostPlatform;
            var cachedHelmPath  = KubeHelper.GetCachedComponentPath(hostPlatform, "helm", KubeVersions.HelmVersion);
            var targetPath      = Path.Combine(KubeHelper.ProgramFolder);

            switch (hostPlatform)
            {
                case KubeHostPlatform.Windows:

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
                            throw new KubeException(error);
                        }

                        pStart += pattern.Length;

                        var pEnd = response.OutputText.IndexOf("\"", pStart);

                        if (pEnd == -1)
                        {
                            throw new KubeException(error);
                        }

                        var currentVersionString = response.OutputText.Substring(pStart, pEnd - pStart);

                        if (!Version.TryParse(currentVersionString, out var currentVersion))
                        {
                            throw new KubeException(error);
                        }

                        if (Version.Parse(KubeVersions.HelmVersion) > currentVersion)
                        {
                            // We need to copy and overwrite with the latest version.

                            if (File.Exists(targetPath))
                            {
                                File.Delete(targetPath);
                            }

                            File.Copy(cachedHelmPath, targetPath);
                        }
                    }
                    break;

                case KubeHostPlatform.Linux:
                case KubeHostPlatform.Osx:
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
            Task.Run(() => NeonHelper.ExecuteAsync("kubectl", 
                args: new string[]
                {
                    "--namespace", @namespace, 
                    "port-forward",
                    $"svc/{serviceName}", 
                    $"{localPort}:{remotePort}" 
                },
                process: process));
        }

        /// <summary>
        /// Executes a command in a k8s pod.
        /// </summary>
        /// <param name="client">The <see cref="Kubernetes"/> client to use.</param>
        /// <param name="pod">The pod where the command should run.</param>
        /// <param name="namespace">The namespace where the pod is running.</param>
        /// <param name="command">The command to run.</param>
        /// <returns>The command result.</returns>
        public async static Task<string> ExecInPod(IKubernetes client, V1Pod pod, string @namespace, string[] command)
        {
            var webSocket = await client.WebSocketNamespacedPodExecAsync(pod.Metadata.Name, @namespace, command, pod.Spec.Containers[0].Name);
            var demux     = new StreamDemuxer(webSocket);

            demux.Start();

            var buff   = new byte[4096];
            var stream = demux.GetStream(1, 1);
            var read   = stream.Read(buff, 0, 4096);

            return Encoding.Default.GetString(buff.Where(b => b != 0).ToArray());
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
        /// Packages the files within a folder into an ISO file.
        /// </para>
        /// <note>
        /// This requires Powershell to be installed and this will favor using the version of
        /// Powershell installed along with the neon-cli is present.
        /// </note>
        /// </summary>
        /// <param name="inputFolder">Path to the input folder.</param>
        /// <param name="isoPath">Path to the output ISO file.</param>
        /// <param name="label">Optionally specifies a volume label.</param>
        /// <exception cref="ExecuteException">Thrown if the operation failed.</exception>
        public static void CreateIsoFile(string inputFolder, string isoPath, string label = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(inputFolder), nameof(inputFolder));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(isoPath), nameof(isoPath));
            Covenant.Requires<ArgumentException>(!inputFolder.Contains('"'), nameof(inputFolder));      // We don't escape quotes below so we'll
            Covenant.Requires<ArgumentException>(!isoPath.Contains('"'), nameof(isoPath));              // reject paths including quotes.

            label = label ?? string.Empty;

            // We're going to use a function from the Microsoft Technet Script Center:
            //
            //      https://gallery.technet.microsoft.com/scriptcenter/New-ISOFile-function-a8deeffd

            const string newIsoFileFunc =
@"function New-IsoFile  
{  
  <#  
   .Synopsis  
    Creates a new .iso file  
   .Description  
    The New-IsoFile cmdlet creates a new .iso file containing content from chosen folders  
   .Example  
    New-IsoFile ""c:\tools"",""c:Downloads\utils""  
    This command creates a .iso file in $env:temp folder (default location) that contains c:\tools and c:\downloads\utils folders. The folders themselves are included at the root of the .iso image.  
   .Example 
    New-IsoFile -FromClipboard -Verbose 
    Before running this command, select and copy (Ctrl-C) files/folders in Explorer first.  
   .Example  
    dir c:\WinPE | New-IsoFile -Path c:\temp\WinPE.iso -BootFile ""${env:ProgramFiles(x86)}\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\efisys.bin"" -Media DVDPLUSR -Title ""WinPE"" 
    This command creates a bootable .iso file containing the content from c:\WinPE folder, but the folder itself isn't included. Boot file etfsboot.com can be found in Windows ADK. Refer to IMAPI_MEDIA_PHYSICAL_TYPE enumeration for possible media types: http://msdn.microsoft.com/en-us/library/windows/desktop/aa366217(v=vs.85).aspx  
   .Notes 
    NAME:  New-IsoFile  
    AUTHOR: Chris Wu 
    LASTEDIT: 03/23/2016 14:46:50  
 #>  
  
  [CmdletBinding(DefaultParameterSetName='Source')]Param( 
    [parameter(Position=1,Mandatory=$true,ValueFromPipeline=$true, ParameterSetName='Source')]$Source,  
    [parameter(Position=2)][string]$Path = ""$env:temp\$((Get-Date).ToString('yyyyMMdd-HHmmss.ffff')).iso"",  
    [ValidateScript({Test-Path -LiteralPath $_ -PathType Leaf})][string]$BootFile = $null, 
    [ValidateSet('CDR','CDRW','DVDRAM','DVDPLUSR','DVDPLUSRW','DVDPLUSR_DUALLAYER','DVDDASHR','DVDDASHRW','DVDDASHR_DUALLAYER','DISK','DVDPLUSRW_DUALLAYER','BDR','BDRE')][string] $Media = 'DVDPLUSRW_DUALLAYER', 
    [string]$Title = (Get-Date).ToString(""yyyyMMdd-HHmmss.ffff""),  
    [switch]$Force, 
    [parameter(ParameterSetName='Clipboard')][switch]$FromClipboard 
  ) 
 
  Begin {  
    ($cp = new-object System.CodeDom.Compiler.CompilerParameters).CompilerOptions = '/unsafe' 
    if (!('ISOFile' -as [type])) {  
      Add-Type -CompilerParameters $cp -TypeDefinition @' 
public class ISOFile  
{ 
  public unsafe static void Create(string Path, object Stream, int BlockSize, int TotalBlocks)  
  {  
    int bytes = 0;  
    byte[] buf = new byte[BlockSize];  
    var ptr = (System.IntPtr)(&bytes);  
    var o = System.IO.File.OpenWrite(Path);  
    var i = Stream as System.Runtime.InteropServices.ComTypes.IStream;  
  
    if (o != null) { 
      while (TotalBlocks-- > 0) {  
        i.Read(buf, BlockSize, ptr); o.Write(buf, 0, bytes);  
      }  
      o.Flush(); o.Close();  
    } 
  } 
}  
'@  
    } 
  
    if ($BootFile) { 
      if('BDR','BDRE' -contains $Media) { Write-Warning ""Bootable image doesn't seem to work with media type $Media"" } 
      ($Stream = New-Object -ComObject ADODB.Stream -Property @{Type=1}).Open()  # adFileTypeBinary 
      $Stream.LoadFromFile((Get-Item -LiteralPath $BootFile).Fullname) 
      ($Boot = New-Object -ComObject IMAPI2FS.BootOptions).AssignBootImage($Stream) 
    } 
 
    $MediaType = @('UNKNOWN','CDROM','CDR','CDRW','DVDROM','DVDRAM','DVDPLUSR','DVDPLUSRW','DVDPLUSR_DUALLAYER','DVDDASHR','DVDDASHRW','DVDDASHR_DUALLAYER','DISK','DVDPLUSRW_DUALLAYER','HDDVDROM','HDDVDR','HDDVDRAM','BDROM','BDR','BDRE') 
 
    Write-Verbose -Message ""Selected media type is $Media with value $($MediaType.IndexOf($Media))"" 
    ($Image = New-Object -com IMAPI2FS.MsftFileSystemImage -Property @{VolumeName=$Title}).ChooseImageDefaultsForMediaType($MediaType.IndexOf($Media)) 
  
    if (!($Target = New-Item -Path $Path -ItemType File -Force:$Force -ErrorAction SilentlyContinue)) { Write-Error -Message ""Cannot create file $Path. Use -Force parameter to overwrite if the target file already exists.""; break } 
  }  
 
  Process { 
    if($FromClipboard) { 
      if($PSVersionTable.PSVersion.Major -lt 5) { Write-Error -Message 'The -FromClipboard parameter is only supported on PowerShell v5 or higher'; break } 
      $Source = Get-Clipboard -Format FileDropList 
    } 
 
    foreach($item in $Source) { 
      if($item -isnot [System.IO.FileInfo] -and $item -isnot [System.IO.DirectoryInfo]) { 
        $item = Get-Item -LiteralPath $item 
      } 
 
      if($item) { 
        Write-Verbose -Message ""Adding item to the target image: $($item.FullName)"" 
        try { $Image.Root.AddTree($item.FullName, $true) } catch { Write-Error -Message ($_.Exception.Message.Trim() + ' Try a different media type.') } 
      } 
    } 
  } 
 
  End {  
    if ($Boot) { $Image.BootImageOptions=$Boot }  
    $Result = $Image.CreateResultImage()  
    [ISOFile]::Create($Target.FullName,$Result.ImageStream,$Result.BlockSize,$Result.TotalBlocks) 
    Write-Verbose -Message ""Target image ($($Target.FullName)) has been created"" 
    $Target 
  } 
} 
";
            // Delete any existing ISO file.

            File.Delete(isoPath);

            // Use the version of Powershell installed along with the neon-cli, if present,
            // otherwise just launch Powershell from the PATH.

            var neonKubeProgramFolder = Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER");
            var powershellPath        = "powershell";

            if (neonKubeProgramFolder != null)
            {
                var path = Path.Combine(neonKubeProgramFolder, powershellPath);

                if (File.Exists(path))
                {
                    powershellPath = path;
                }
            }

            // Generate a temporary script file and run it.

            using (var tempFile = new TempFile(suffix: ".ps1"))
            {
                var script = newIsoFileFunc;

                script += $"Get-ChildItem \"{inputFolder}\" | New-ISOFile -path \"{isoPath}\" -Title \"{label}\"";

                File.WriteAllText(tempFile.Path, script);

                var result = NeonHelper.ExecuteCapture(powershellPath,
                    new object[]
                    {
                        "-f", tempFile.Path
                    });

                result.EnsureSuccess();
            }
        }

        /// <summary>
        /// Creates an ISO file containing the <b>neon-node-prep.sh</b> script that 
        /// will be used for confguring the node on first boot.  This includes disabling
        /// the APT package update services, setting a secure password for the [sysadmin]
        /// account, and configuring the network interface with the configured static IP.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeDefinition">The node definition.</param>
        /// <param name="securePassword">The new secure SSH password.</param>
        /// <returns>A <see cref="TempFile"/> that references the generated ISO file.</returns>
        /// <remarks>
        /// <para>
        /// The hosting manager will call this for each node being prepared and then
        /// insert the ISO into the node VM's DVD/CD drive before booting the node
        /// for the first time.  The <b>neon-node-prep</b> service configured on
        /// the corresponding node templates will look for this DVD and script and
        /// execute it early during the node boot process.
        /// </para>
        /// <para>
        /// The ISO file reference is returned as a <see cref="TempFile"/>.  The
        /// caller should call <see cref="TempFile.Dispose()"/> when it's done
        /// with the file to ensure that it is deleted.
        /// </para>
        /// </remarks>
        public static TempFile CreateNodePrepIso(
            ClusterDefinition       clusterDefinition,
            NodeDefinition          nodeDefinition,
            string                  securePassword)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(securePassword), nameof(securePassword));

            var address       = nodeDefinition.Address;
            var gateway       = clusterDefinition.Network.Gateway;
            var premiseSubnet = NetworkCidr.Parse(clusterDefinition.Network.PremiseSubnet);
            var nameservers   = clusterDefinition.Network.Nameservers;
            var sbNameservers = new StringBuilder();

            // Generate the [neon-node-prep.sh] script.

            foreach (var nameserver in nameservers)
            {
                sbNameservers.AppendWithSeparator(nameserver.ToString(), ",");
            }

            var nodePrepScript =
$@"# This script is called by the [neon-node-prep] service when the prep
# DVD is inserted on first boot.  This script handles configuring the
# network.
#
# The first parameter will be passed as the path where the DVD is mounted.

mountFolder=${{1}}

#------------------------------------------------------------------------------
# Sleep for a bit in an attempt to ensure that the system is actually ready.
#
#       https://github.com/nforgeio/neonKUBE/issues/980

sleep 10

#------------------------------------------------------------------------------
# Disable the [apt-timer] and [apt-daily] services.  We're doing this 
# for two reasons:
#
#   1. These services interfere with with [apt-get] usage during
#      cluster setup and is also likely to interfere with end-user
#      configuration activities as well.
#
#   2. Automatic updates for production and even test clusters is
#      just not a great idea.  You just don't want a random update
#      applied in the middle of the night which might cause trouble.
#
#      We're going to implement our own cluster updating machanism
#      that will be smart enough to update the nodes such that the
#      impact on cluster workloads will be limited.

systemctl stop apt-daily.timer
systemctl mask apt-daily.timer

systemctl stop apt-daily.service
systemctl mask apt-daily.service

# It may be possible for the auto updater to already be running so we'll
# wait here for it to release any lock files it holds.

while fuser /var/{{lib /{{dpkg,apt/lists}},cache/apt/archives}}/lock; do
    sleep 1
done

#------------------------------------------------------------------------------
# Change the [sysadmin] user password from the hardcoded [sysadmin0000] password
# to something secure.  Doing this here before the network is configured means 
# that there's no time when bad guys can SSH into the node using the insecure
# password.

echo 'sysadmin:{securePassword}' | chpasswd

#------------------------------------------------------------------------------
# Configure the network.

echo ""Configure network: {address}""

rm /etc/netplan/*

cat <<EOF > /etc/netplan/static.yaml
# Static network configuration initialized during first boot by the 
# [neon-node-prep] service from a virtual ISO inserted during
# cluster prepare.

network:
  version: 2
  renderer: networkd
  ethernets:
    eth0:
     dhcp4: no
     addresses: [{address}/{premiseSubnet.PrefixLength}]
     gateway4: {gateway}
     nameservers:
       addresses: [{sbNameservers}]
EOF

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
exit 0
";
            nodePrepScript = nodePrepScript.Replace("\r\n", "\n");  // Linux line endings

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
                File.WriteAllText(Path.Combine(tempFolder.Path, "neon-node-prep.sh"), nodePrepScript);

                // Note that the ISO needs to be created in an unencrypted folder
                // (not /USER/neonkube/...) so that Hyper-V can mount it to a VM.
                //
                var isoFile = new TempFile(suffix: ".iso", folder: orgTempPath);

                KubeHelper.CreateIsoFile(tempFolder.Path, isoFile.Path, "cidata");

                return isoFile;
            }
        }

#if NO_BUILD
        // $note(jefflill):
        //
        // I'm commenting this out because we went with DVDs rather than floppy discs
        // for first boot node node configuration on hypervisors but I want to keep
        // this around in case we want to do something like this again in the future.

        /// <summary>
        /// Creates an floppy VFD file containing the <b>neon-node-prep.sh</b> script that 
        /// will provide the information to <b>cloud-init</b> when cluster nodes
        /// are prepared on non-cloud virtualization hosts like Hyper-V and XenServer.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeDefinition">The node definition.</param>
        /// <param name="nodePrepScript">The node preparation script.</param>
        /// <returns>A <see cref="TempFile"/> that references the generated VFD file.</returns>
        /// <remarks>
        /// <para>
        /// The hosting manager will call this for each node being prepared and then
        /// insert the ISO into the node VM's floppy drive before booting the node
        /// for the first time.  The <b>neon-node-prep</b> service configured on
        /// the corresponding node templates will look for this DVD and script and
        /// execute it early during the node boot process, before <b>cloud-init</b>
        /// runs.  When <b>cloud-init</b> runs, it will complete the basic node
        /// preparation.
        /// </para>
        /// <para>
        /// The VFD file reference is returned as a <see cref="TempFile"/>.  The
        /// caller should call <see cref="TempFile.Dispose()"/> when it's done
        /// with the file to ensure that it is deleted.
        /// </para>
        /// <note>
        /// This method uses a special purpose floppy drive image generated via the
        /// <b>neon generate init-floppy</b> command.
        /// </note>
        /// </remarks>
        public static TempFile CreateNodePrepVfd(
            ClusterDefinition       clusterDefinition,
            NodeDefinition          nodeDefinition,
            string                  nodePrepScript)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodePrepScript), nameof(nodePrepScript));

            // This is the special purpose VFD file contents compressed via GZIP.

            var vfdGzip = new byte[]
            {
                0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0a, 0xed, 0xdc, 0x4d, 0x6f, 0xd4, 0x45, 0x18, 0x00, 0xf0, 0xd9, 0xb7, 0x42, 0x5b, 0x4b, 0x79, 0x11, 0x10, 0x11, 0xf9, 0x03,
                0x22, 0x56, 0xec, 0x42, 0x8b, 0x88, 0x08, 0xc8, 0x02, 0xf2, 0x26, 0x52, 0x8a, 0x50, 0x10, 0x11, 0xd8, 0x2d, 0x5d, 0xa4, 0x01, 0xb7, 0x4d, 0x77, 0x2f, 0x24, 0xc6, 0xf4, 0x13, 0x18,
                0x13, 0xbf, 0x40, 0xcf, 0xc6, 0xa3, 0x17, 0x43, 0x62, 0xf6, 0xe8, 0x85, 0x3b, 0xc7, 0x3d, 0x70, 0x23, 0x31, 0x7c, 0x02, 0xeb, 0x56, 0x0e, 0xd6, 0xa3, 0x89, 0xd1, 0x84, 0xe7, 0xf7,
                0xcb, 0x64, 0xe6, 0xc9, 0x3c, 0x99, 0xc9, 0x4c, 0xe6, 0x36, 0x99, 0xcc, 0xd3, 0xc3, 0xdf, 0x7d, 0x75, 0xef, 0x4e, 0xb3, 0x7c, 0xa7, 0xd6, 0x4a, 0xf9, 0x5c, 0x2e, 0xe5, 0x3b, 0xa9,
                0xd2, 0xff, 0xac, 0x37, 0xad, 0x4d, 0xf9, 0xf4, 0x97, 0xa1, 0xdf, 0xbe, 0x7f, 0x32, 0x7a, 0x7b, 0x7a, 0xaa, 0xd6, 0xaa, 0x65, 0x4b, 0x4e, 0x1d, 0xbb, 0x3c, 0x32, 0xda, 0x6d, 0x57,
                0x6d, 0xfd, 0xe5, 0xfa, 0xd7, 0x3f, 0x6e, 0x6f, 0xb7, 0xfa, 0xaf, 0xfc, 0xb4, 0xea, 0xe1, 0x8a, 0xf4, 0x68, 0xf5, 0xcd, 0xa7, 0xcf, 0x46, 0x9f, 0x3c, 0xda, 0xf0, 0x68, 0xd3, 0xd3,
                0xdf, 0x2f, 0xdf, 0x9d, 0x6e, 0x66, 0xdd, 0xd2, 0x98, 0x69, 0x65, 0xb5, 0x6c, 0x72, 0x66, 0xa6, 0x55, 0x9b, 0xbc, 0x5f, 0xcf, 0xa6, 0xa6, 0x9b, 0xf7, 0xca, 0x59, 0x36, 0x7e, 0xbf,
                0x5e, 0x6b, 0xd6, 0xb3, 0xe9, 0x46, 0xb3, 0x3e, 0xf7, 0xb7, 0xfc, 0x9d, 0xfb, 0x33, 0xb3, 0xb3, 0x0f, 0xb2, 0x5a, 0x63, 0x6a, 0xa0, 0x6f, 0x76, 0xae, 0xde, 0x6c, 0x76, 0xc3, 0x07,
                0xd9, 0xbd, 0xfa, 0x83, 0xac, 0x35, 0x93, 0xb5, 0xe6, 0xba, 0x99, 0x2f, 0x6b, 0xd3, 0x8d, 0xac, 0x5c, 0x2e, 0x67, 0x03, 0x7d, 0xcb, 0xd6, 0xc8, 0x3f, 0x37, 0xf1, 0xc3, 0xb3, 0xc5,
                0xc5, 0x54, 0x49, 0xa5, 0x6a, 0x5a, 0x31, 0x9f, 0x7a, 0x17, 0x52, 0x7f, 0x3b, 0x0d, 0x74, 0xd2, 0x60, 0xca, 0xad, 0xc9, 0x72, 0xeb, 0x2a, 0xb9, 0xf5, 0xd5, 0xdc, 0xc6, 0xf9, 0xdc,
                0xa6, 0x85, 0xdc, 0xe6, 0x76, 0x6e, 0x4b, 0x27, 0xb7, 0x35, 0xe5, 0xb7, 0x65, 0xf9, 0x1d, 0x95, 0xfc, 0xce, 0x6a, 0x7e, 0xd7, 0x7c, 0x7e, 0x68, 0x21, 0xbf, 0xbb, 0x9d, 0x1f, 0xee,
                0xe4, 0xf7, 0xa4, 0xc2, 0x48, 0x56, 0xd8, 0x57, 0x29, 0xec, 0xaf, 0x16, 0x0e, 0xcc, 0x17, 0x0e, 0x2e, 0x14, 0x0e, 0xb5, 0x0b, 0x47, 0x3a, 0x85, 0xa3, 0xa9, 0x78, 0x2c, 0x2b, 0x9e,
                0xa8, 0x14, 0x4f, 0x56, 0x8b, 0xa7, 0xe7, 0x8b, 0x67, 0x17, 0x8a, 0xe7, 0xda, 0xc5, 0xf3, 0x9d, 0xe2, 0x85, 0x54, 0xba, 0x98, 0x95, 0x2e, 0x55, 0x4a, 0x13, 0xd5, 0xd2, 0xd5, 0xf9,
                0xd2, 0xb5, 0x85, 0xd2, 0xf5, 0x76, 0xe9, 0x46, 0xa7, 0x74, 0x2b, 0xf5, 0xd4, 0xb2, 0x9e, 0xdb, 0x95, 0x9e, 0x7a, 0xb5, 0x67, 0x71, 0xd0, 0x81, 0x02, 0x00, 0x00, 0x00, 0xfc, 0x0b,
                0xdc, 0xff, 0x00, 0x00, 0x00, 0x00, 0xbc, 0xd8, 0x96, 0x3d, 0xea, 0x5a, 0x99, 0xd2, 0xcf, 0xdf, 0x3e, 0x1e, 0x7f, 0x3c, 0xfe, 0xbc, 0x7d, 0x9e, 0x3f, 0x3e, 0x9b, 0xca, 0xa9, 0x99,
                0xee, 0x76, 0xc3, 0xc1, 0xd4, 0x58, 0x5c, 0x26, 0xa5, 0xa5, 0x3a, 0xd7, 0x48, 0xf5, 0x34, 0x93, 0x1a, 0x69, 0x78, 0x29, 0xdf, 0xe8, 0x86, 0x53, 0xdd, 0x8e, 0xe1, 0x34, 0xdb, 0x1d,
                0x30, 0xd7, 0x8d, 0xc6, 0x4e, 0x5e, 0x18, 0x1b, 0x1e, 0xfb, 0x66, 0xe4, 0xd2, 0x99, 0x2c, 0x4b, 0x7b, 0x97, 0xcf, 0x5f, 0x48, 0xe9, 0xd7, 0xff, 0x73, 0xe7, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x2f, 0x96, 0x1c, 0xa1, 0xe5, 0x09, 0xad, 0x40, 0x68, 0x45, 0x42, 0x2b, 0x11, 0x5a, 0x0f, 0xa1, 0xad, 0x20, 0xb4, 0x95, 0x84, 0xd6, 0x4b, 0x68, 0x7d, 0x84, 0xd6,
                0x4f, 0x68, 0x2f, 0x11, 0xda, 0x00, 0xa1, 0xad, 0x22, 0xb4, 0x41, 0x42, 0x5b, 0x4d, 0x68, 0x6b, 0x08, 0x6d, 0x2d, 0xa1, 0xad, 0x23, 0xb4, 0x97, 0x09, 0x6d, 0x3d, 0xa1, 0x6d, 0x20,
                0xb4, 0x8d, 0x84, 0xf6, 0x0a, 0xa1, 0x6d, 0x22, 0xb4, 0x57, 0x09, 0x6d, 0x33, 0xa1, 0xbd, 0x46, 0x68, 0x5b, 0x08, 0xed, 0x75, 0x42, 0xdb, 0x4a, 0x68, 0x7f, 0xfe, 0xfc, 0x4d, 0x58,
                0xdb, 0x08, 0x6d, 0x3b, 0xa1, 0xed, 0x20, 0xb4, 0x37, 0x08, 0x6d, 0x27, 0xa1, 0xbd, 0x49, 0x68, 0xbb, 0x08, 0xed, 0x2d, 0x42, 0x1b, 0x22, 0xb4, 0xb7, 0x09, 0x6d, 0x37, 0xa1, 0xbd,
                0x43, 0x68, 0xc3, 0x84, 0x56, 0x26, 0xb4, 0x3d, 0x84, 0xb6, 0x97, 0xd0, 0x46, 0x08, 0x6d, 0x94, 0xd0, 0xf6, 0x11, 0xda, 0xbb, 0x84, 0xb6, 0x9f, 0xd0, 0xde, 0x23, 0xb4, 0x03, 0x84,
                0xf6, 0x3e, 0xa1, 0x1d, 0x24, 0xb4, 0x0f, 0x08, 0xed, 0x10, 0xa1, 0x1d, 0x26, 0xb4, 0x23, 0x84, 0xf6, 0x21, 0xa1, 0x1d, 0x25, 0xb4, 0x0a, 0xa1, 0x1d, 0x23, 0xb4, 0xe3, 0x84, 0x76,
                0x82, 0xd0, 0x3e, 0x22, 0xb4, 0x93, 0x84, 0x76, 0x8a, 0xd0, 0x4e, 0x13, 0xda, 0x19, 0x42, 0x3b, 0x4b, 0x68, 0x1f, 0x13, 0xda, 0x39, 0x42, 0xfb, 0x84, 0xd0, 0xce, 0x13, 0xda, 0x18,
                0xa1, 0x5d, 0x20, 0xb4, 0x71, 0x42, 0xbb, 0x48, 0x68, 0x9f, 0x12, 0xda, 0x25, 0x42, 0xbb, 0x4c, 0x68, 0x13, 0x84, 0x76, 0x85, 0xd0, 0xae, 0x12, 0xda, 0x67, 0x84, 0x76, 0x8d, 0xd0,
                0x3e, 0x27, 0xb4, 0xeb, 0x84, 0xf6, 0x05, 0xa1, 0xdd, 0x20, 0xb4, 0x9b, 0x84, 0x76, 0x8b, 0xd0, 0xaa, 0x84, 0x56, 0x23, 0xb4, 0x49, 0x42, 0xbb, 0x4d, 0x68, 0x53, 0x84, 0x96, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xf8, 0x4f, 0xfd, 0x01, 0xed, 0xb1, 0x6b, 0xfc, 0x00, 0x80, 0x16, 0x00,
            };

            var vfdBytes      = NeonHelper.GunzipBytes(vfdGzip);
            var address       = nodeDefinition.PrivateAddress;
            var gateway       = clusterDefinition.Network.Gateway;
            var premiseSubnet = NetworkCidr.Parse(clusterDefinition.Network.PremiseSubnet);
            var nameservers   = clusterDefinition.Network.Nameservers;
            var sbNameservers = new StringBuilder();

            // Generate the [neon-node-prep.sh] script.

            foreach (var nameserver in nameservers)
            {
                sbNameservers.AppendWithSeparator(nameserver.ToString(), ",");
            }

            nodePrepScript = nodePrepScript.Replace("\r\n", "\n");  // Linux line endings

            // This is the tricky part.  The floppy image contains only the [neon-node-prep.sh]
            // file consisting of 100 512B blocks of data.  The first file block is filled with
            // 0x01, the second with 0x01, and so on with the last block filled with 0x64 (100).
            //
            // We're going to scan the VFD image for identify these blocks and then:
            //
            //      1. Fill all blocks with 0x0a (LINEFEED) characters and then
            //      2. Go back and write the script bytes (UTF-8) to the blocks
            //
            // Note that device blocks always start at 512 intervals so this will be a simple scan.

            const int vfdBlocks     = 2880;     // # of 512B blocks on a floppy
            const int blockCount    = 100;      // [neon-node-prep.sh] block count
            const int blockSize     = 512;      // floppy block size
            const int maxScriptSize = blockCount * blockSize;

            var scriptBytes = Encoding.UTF8.GetBytes(nodePrepScript);
            var blockPos    = new int[blockCount];

            Covenant.Assert(scriptBytes.Length < maxScriptSize, $"[neon-node-prep.sh] script cannot exceed [{maxScriptSize}] bytes.");

            for (int i = 0; i < blockPos.Length; i++)
            {
                blockPos[i] = -1;
            }

            // Scan for the file block positions.

            for (int block = 0; block < vfdBlocks; block++)
            {
                var firstBlockByte = vfdBytes[block * blockSize];

                if (firstBlockByte == 0 || firstBlockByte > 100)
                {
                    // This cannot be a [neon-node-prep.sh] block.

                    continue;
                }

                var blockIndex    = (int)vfdBytes[block * blockSize];
                var isScriptBlock = true;

                for (int i = block * blockSize; i < (block + 1) * blockSize; i++)
                {
                    if (vfdBytes[i] != blockIndex)
                    {
                        // Script blocks need to be filled with the file's block index byte pattern.

                        isScriptBlock = false;
                        break;
                    }
                }

                if (isScriptBlock)
                {
                    blockPos[blockIndex - 1] = block;
                }
            }

            // Ensure that we found all of the blocks.

            Covenant.Assert(!blockPos.Any(pos => pos == -1), "Invalid VFD image: [neon-node-prep.sh] is missing one or more blocks.");

            // Overwrite all of the [neon-node-prep.sh] file blocks with LINEFEEDs.

            foreach (var block in blockPos)
            {
                for (int i = block * blockSize; i < (block + 1) * blockSize; i++)
                {
                    vfdBytes[i] = 0x0A;
                }
            }

            // Write the script bytes to the VFD image's [neon-node-prep.sh] file blocks.

            for (int scriptBlock = 0; scriptBlock < blockPos.Length; scriptBlock++)
            {
                var vfdPos    = blockPos[scriptBlock] * blockSize;
                var scriptPos = scriptBlock * blockSize;

                for (int i = scriptPos; i < Math.Min(scriptPos + blockSize, scriptBytes.Length); i++)
                {
                    vfdBytes[vfdPos + i - scriptPos] = scriptBytes[i]; 
                }

                if (scriptPos + blockSize >= scriptBytes.Length)
                {
                    // Finished writing the script bytes.

                    break;
                }
            }

            // Create a VFD file that includes the script and return its TempFile.
            //
            // NOTE:
            //
            // that the VFD needs to be created in an unencrypted folder so that Hyper-V 
            // can mount it to a VM.  By default, [neon-cli] will redirect the [TempFolder] 
            // and [TempFile] classes locate their folder and files here:
            //
            //      /USER/.neonkube/...     - which is encrypted on Windows

            var orgTempPath = Path.GetTempPath();
            var vfdFile     = new TempFile(folder: orgTempPath, suffix: ".vfd");

            File.WriteAllBytes(vfdFile.Path, vfdBytes);

            return vfdFile;
        }
#endif // NO_BUILD

        /// <summary>
        /// Writes a message to a log writer action when it's not <c>null</c>.
        /// </summary>
        /// <param name="logWriter">The log writer action ot <c>null</c>.</param>
        /// <param name="message">The message or <c>null</c> to wtite a blank line.</param>
        private static void WriteLog(Action<string> logWriter, string message = null)
        {
            if (logWriter != null)
            {
                logWriter(message ?? string.Empty);
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
            // This will be installed with neonDESKTOP and is also available as part of the
            // neonKUBE Git repo as a fall back for Neon developers that haven't installed 
            // the desktop yet.

            // Look for the installed version first.

            var desktopProgramFolder = Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER");
            var path1                = desktopProgramFolder != null ? Path.Combine(Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER"), "SSH", "ssh-keygen.exe") : null;

            if (path1 != null && File.Exists(path1))
            {
                return Path.GetFullPath(path1);
            }

            // Fall back to the executable from our Git repo.

            var repoFolder = Environment.GetEnvironmentVariable("NF_ROOT");
            var path2      = repoFolder != null ? Path.Combine(repoFolder, "External", "SSH", "ssh-keygen.exe") : null;

            if (path2 != null && File.Exists(path2))
            {
                return Path.GetFullPath(path2);
            }

            throw new FileNotFoundException($"Cannot locate [ssh-keygen.exe] at [{path1}] or [{path2}].");
        }

        /// <summary>
        /// Creates a SSH key for a neonKUBE cluster.
        /// </summary>
        /// <param name="clusterName">The cluster name.</param>
        /// <param name="userName">Optionally specifies the user name (defaults to <b>root</b>).</param>
        /// <returns>A <see cref="KubeSshKey"/> holding the public and private parts of the key.</returns>
        public static KubeSshKey GenerateSshKey(string clusterName, string userName = "root")
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
                    throw new KubeException("Cannot generate SSH key:\r\n\r\n" + result.AllText);
                }

                var publicPUB      = File.ReadAllText(Path.Combine(tempFolder.Path, "key.pub"));
                var privateOpenSSH = File.ReadAllText(Path.Combine(tempFolder.Path, "key"));

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
                    throw new KubeException("Cannot convert SSH public key to PEM:\r\n\r\n" + result.AllText);
                }

                var publicOpenSSH = result.OutputText;

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
                    throw new KubeException("Cannot convert SSH public key to SSH2:\r\n\r\n" + result.AllText);
                }

                var publicSSH2 = result.OutputText;

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

                publicSSH2 = sbPublicSSH2.ToString();

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
                    throw new KubeException("Cannot convert SSH private key to PEM:\r\n\r\n" + result.AllText);
                }

                var privatePEM = File.ReadAllText(Path.Combine(tempFolder.Path, "key.pem"));

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
                    throw new KubeException("Cannot generate SSH public key MD5 fingerprint:\r\n\r\n" + result.AllText);
                }

                var fingerprintMd5 = result.OutputText.Trim();

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
                    throw new KubeException("Cannot generate SSH public key SHA256 fingerprint:\r\n\r\n" + result.AllText);
                }

                var fingerprintSha2565 = result.OutputText.Trim();

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
        /// Performs low-level initialization of a cluster node.  This is applied one time to
        /// Hyper-V and XenServer/XCP-ng node templates when they are created and at cluster
        /// creation time for cloud and bare metal based clusters.  The node must already
        /// be booted and running.
        /// </summary>
        /// <param name="node">The node's SSH proxy.</param>
        /// <param name="sshPassword">The current <b>sysadmin</b> password.</param>
        /// <param name="updateDistribution">Optionally upgrade the node's Linux distribution.  This defaults to <c>false</c>.</param>
        /// <param name="logWriter">Action that writes a line of text to the operation output log or console (or <c>null</c>).</param>
        public static void InitializeNode(SshProxy<NodeDefinition> node, string sshPassword, bool updateDistribution = false, Action<string> logWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sshPassword), nameof(sshPassword));

            // $hack(jefflill):
            //
            // This method is going to be called for two different scenarios that will each
            // call for different logging mechanisms.
            //
            //      1. For the [neon prepare node-termplate] command, we're simply going 
            //         to write status to the console as lines via the [logWriter].
            //
            //      2. For node preparation for cloud and bare metal clusters, we're
            //         going to set the node status and use the standard setup progress
            //         mechanism to display the status.
            //
            // [logWriter] will be NULL for the second scenario so we'll call the log helper
            // method above which won't do anything.
            //
            // For scenario #1, there is no setup display mechanism, so updating node status
            // won't actually display anything, so we'll just set the status as well without
            // harming anything.

            // Wait for boot/connect.

            WriteLog(logWriter, $"Login:    [{KubeConst.SysAdminUsername}]");
            node.Status = $"login: [{KubeConst.SysAdminUsername}]";

            node.WaitForBoot();

            // Disable and mask the auto update services to avoid conflicts with
            // our package operations.  We're going to implement our own cluster
            // updating mechanism.

            WriteLog(logWriter, $"Disable:  auto updates");
            node.Status = "disable: auto updates";

            node.SudoCommand("systemctl stop apt-daily.timer", RunOptions.None);
            node.SudoCommand("systemctl mask apt-daily.timer", RunOptions.None);

            node.SudoCommand("systemctl stop apt-daily.service", RunOptions.None);
            node.SudoCommand("systemctl mask apt-daily.service", RunOptions.None);

            // Wait for the apt-get lock to be released if somebody is holding it.

            WriteLog(logWriter, $"Wait:     for pending updates");
            node.Status = "wait: for pending updates";

            while (node.SudoCommand("fuser /var/{lib/{dpkg,apt/lists},cache/apt/archives}/lock", RunOptions.None).ExitCode == 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            // Disable sudo password prompts and reconnect.

            WriteLog(logWriter, "Disable:  [sudo] password");
            node.Status = "disable: sudo password";
            node.DisableSudoPrompt(sshPassword);

            WriteLog(logWriter, $"Login:    [{KubeConst.SysAdminUsername}]");
            node.Status = "reconnecting...";
            node.WaitForBoot();

            // Install required packages and ugrade the distribution if requested.

            WriteLog(logWriter, "Install:  packages");
            node.Status = "install: packages";
            node.SudoCommand("apt-get update", RunOptions.FaultOnError);
            node.SudoCommand("apt-get install -yq --allow-downgrades zip secure-delete", RunOptions.FaultOnError);

            if (updateDistribution)
            {
                WriteLog(logWriter, "Upgrade:  linux");
                node.Status = "upgrade linux";
                node.SudoCommand("apt-get dist-upgrade -yq");
            }

            // Disable SWAP by editing [/etc/fstab] to remove the [/swap.img] line.

            WriteLog(logWriter, "Disable:  swap");
            node.Status = "disable: swap";

            var sbFsTab = new StringBuilder();

            using (var reader = new StringReader(node.DownloadText("/etc/fstab")))
            {
                foreach (var line in reader.Lines())
                {
                    if (!line.Contains("/swap.img"))
                    {
                        sbFsTab.AppendLine(line);
                    }
                }
            }

            node.UploadText("/etc/fstab", sbFsTab, permissions: "644", owner: "root:root");

            // We need to relocate the [sysadmin] UID/GID to 1234 so we
            // can create the [container] user and group at 1000.  We'll
            // need to create a temporary user with root permissions to
            // delete and then recreate the [sysadmin] account.

            WriteLog(logWriter, "Create:   [temp] user");
            node.Status = "create: [temp] user";

            var tempUserScript =
$@"#!/bin/bash

# Create the [temp] user.

useradd --uid 5000 --create-home --groups root temp
echo 'temp:{sshPassword}' | chpasswd
chown temp:temp /home/temp

# Add [temp] to the same groups that [sysadmin] belongs to
# other than the [sysadmin] group.

adduser temp adm
adduser temp cdrom
adduser temp sudo
adduser temp dip
adduser temp plugdev
adduser temp lxd
";
            node.SudoCommand(CommandBundle.FromScript(tempUserScript), RunOptions.FaultOnError);

            // Reconnect with the [temp] account so we can relocate the [sysadmin]
            // user and its group ID to ID=1234.

            WriteLog(logWriter, $"Login:    [temp]");
            node.Status = "login: [temp]";

            node.UpdateCredentials(SshCredentials.FromUserPassword("temp", sshPassword));
            node.Connect();

            // Beginning with Ubuntu 20.04 we're seeing [systemd/(sd-pam)] processes 
            // hanging around for a while for the [sysadmin] user which prevents us 
            // from deleting the [temp] user below.  We're going to handle this by
            // killing any [temp] user processes first.

            WriteLog(logWriter, "Kill:     [sysadmin] user processes");
            node.Status = "kill: [sysadmin] processes";
            node.SudoCommand("pkill -u sysadmin --signal 9");

            // Relocate the [sysadmin] user to from [uid=1000:gid=1000} to [1234:1234]:

            var sysadminUserScript =
$@"#!/bin/bash

# Update all file references from the old to new [sysadmin]
# user and group IDs:

find / -group 1000 -exec chgrp -h {KubeConst.SysAdminGroup} {{}} \;
find / -user 1000 -exec chown -h {KubeConst.SysAdminUsername} {{}} \;

# Relocate the [sysadmin] UID and GID:

groupmod --gid {KubeConst.SysAdminGID} {KubeConst.SysAdminGroup}
usermod --uid {KubeConst.SysAdminUID} --gid {KubeConst.SysAdminGID} --groups root,sysadmin,sudo {KubeConst.SysAdminUsername}
";
            WriteLog(logWriter, "Relocate: [sysadmin] user/group IDs");
            node.Status = "relocate: [sysadmin] user/group IDs";
            node.SudoCommand(CommandBundle.FromScript(sysadminUserScript), RunOptions.FaultOnError);

            WriteLog(logWriter, $"Logout");
            node.Status = "logout";

            // We need to reconnect again with [sysadmin] so we can remove
            // the [temp] user, create the [container] user and then
            // wrap things up.

            node.SudoCommand(CommandBundle.FromScript(tempUserScript), RunOptions.FaultOnError);
            WriteLog(logWriter, $"Login:    [{KubeConst.SysAdminUsername}]");
            node.Status = $"login: [{KubeConst.SysAdminUsername}]";

            node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUsername, sshPassword));
            node.Connect();

            // Beginning with Ubuntu 20.04 we're seeing [systemd/(sd-pam)] processes 
            // hanging around for a while for the [temp] user which prevents us 
            // from deleting the [temp] user below.  We're going to handle this by
            // killing any [temp] user processes first.

            WriteLog(logWriter, "Kill:     [temp] user processes");
            node.Status = "kill: [temp] user processes";
            node.SudoCommand("pkill -u temp");

            // Remove the [temp] user.

            WriteLog(logWriter, "Remove:   [temp] user");
            node.Status = "remove: [temp] user";
            node.SudoCommand($"rm -rf /home/temp", RunOptions.FaultOnError);

            // Ensure that the owner and group for files in the [sysadmin]
            // home folder are correct.

            WriteLog(logWriter, "Set:      [sysadmin] home folder owner");
            node.Status = "set: [sysadmin] home folder owner";
            node.SudoCommand($"chown -R {KubeConst.SysAdminUsername}:{KubeConst.SysAdminGroup} .*", RunOptions.FaultOnError);

            // Create the [container] user with no home directory.  This
            // means that the [container] user will have no chance of
            // logging into the machine.

            WriteLog(logWriter, $"Create:   [{KubeConst.ContainerUsername}] user");
            node.Status = $"create: [{KubeConst.ContainerUsername}] user";
            node.SudoCommand($"useradd --uid {KubeConst.ContainerUID} --no-create-home {KubeConst.ContainerUsername}", RunOptions.FaultOnError);
        }

        /// <summary>
        /// Ensures that the node operating system and version is supported for a neonKUBE
        /// cluster.  This faults the nodeproxy on faliure.
        /// </summary>
        /// <param name="node">The target node.</param>
        internal static void VerifyNodeOperatingSystem(SshProxy<NodeDefinition> node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            node.Status = "check: OS";

            // $todo(jefflill): We're currently hardcoded to Ubuntu 20.04.x

            if (!node.OsName.Equals("Ubuntu", StringComparison.InvariantCultureIgnoreCase) || node.OsVersion < Version.Parse("20.04"))
            {
                node.Fault("Expected: Ubuntu 20.04+");
            }
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
        /// <para>
        /// Ensures that the cluster has at least one OpenEBS node.
        /// </para>
        /// <note>
        /// This doesn't work for the <see cref="HostingEnvironment.BareMetal"/> hosting manager which
        /// needs to actually look for unpartitioned block devices that can be used to provision cStore.
        /// </note>
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <remarks>
        /// This method does nothing if the user has already specified one or more OpenEBS nodes
        /// in the cluster definition.  Otherwise, it will try to enable this on up to three nodes,
        /// trying to avoid master nodes if possible.
        /// </remarks>
        /// <exception cref="NotSupportedException">Thrown for the <see cref="HostingEnvironment.BareMetal"/> hosting manager.</exception>
        public static void EnsureOpenEbsNodes(ClusterDefinition clusterDefinition)
        {
            if (clusterDefinition.Hosting.Environment == HostingEnvironment.BareMetal)
            {
                throw new NotSupportedException($"[{nameof(EnsureOpenEbsNodes)}()] is not supported for the [{nameof(HostingEnvironment.BareMetal)}] hosting manager.");
            }

            if (clusterDefinition.Nodes.Any(node => node.OpenEBS))
            {
                // The user has already selected the nodes.

                return;
            }

            if (clusterDefinition.Workers.Count() >= 3)
            {
                // We have enough workers.

                foreach (var worker in clusterDefinition.SortedWorkerNodes.Take(3))
                {
                    worker.OpenEBS = true;
                }
            }
            else
            {
                // We don't have enough workers, so select the workers we have and
                // then fall back to masters.

                foreach (var node in clusterDefinition.Workers)
                {
                    node.OpenEBS = true;
                }

                foreach (var node in clusterDefinition.SortedMasterNodes.Take(3 - clusterDefinition.Workers.Count()))
                {
                    node.OpenEBS = true;
                }
            }
        }
    }
}
