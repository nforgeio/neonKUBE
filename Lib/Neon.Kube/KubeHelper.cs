//-----------------------------------------------------------------------------
// FILE:	    KubeHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Windows;

namespace Neon.Kube
{
    /// <summary>
    /// cluster related utilties.
    /// </summary>
    public static partial class KubeHelper
    {
        /// <summary>
        /// The root Kubernetes context username for provisioned clusters. 
        /// </summary>
        public const string RootUser = "root";

        private static INeonLogger          log = LogManager.Default.GetLogger(typeof(KubeHelper));
        private static KubeConfig           cachedKubeConfig;
        private static KubeConfigContext    cachedKubeContext;
        private static HeadendClient        cachedHeadendClient;

        /// <summary>
        /// Explicitly sets the class <see cref="INeonLogger"/> implementation.  This defaults to
        /// a reasonable value.
        /// </summary>
        /// <param name="log"></param>
        public static void SetLogger(INeonLogger log)
        {
            Covenant.Requires<ArgumentNullException>(log != null);

            KubeHelper.log = log;
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
        /// Holds the Kubernetes setup information for the cluster setup task. 
        /// </summary>
        public static KubeSetupInfo SetupInfo { get; set; }

        /// <summary>
        /// Returns the path the folder holding the user specific cluster files.
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

                return path;
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".neonkube");

                Directory.CreateDirectory(path);

                return path;
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

                return path;
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".kube");

                Directory.CreateDirectory(path);

                return path;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the root path where the [neon run CMD ...] will copy secrets and run the command.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string RunFolder
        {
            get
            {
                var path = Path.Combine(GetNeonKubeUserFolder(), "run");

                Directory.CreateDirectory(path);

                return path;
            }
        }

        /// <summary>
        /// Returns the path to the Kubernetes configuration file.
        /// </summary>
        public static string KubeConfigPath => Path.Combine(KubeHelper.GetKubeUserFolder(), "config");

        /// <summary>
        /// Returns the path the folder containing cluster related files (including kube context 
        /// extension), creating the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// <para>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage clusters.  Each known cluster will have a JSON file named
        /// <b><i>NAME</i>.context.json</b> holding the serialized <see cref="KubeContextExtension"/> 
        /// information for the cluster, where <i>NAME</i> maps to a cluster configuration name
        /// within the <c>kubeconfig</c> file.
        /// </para>
        /// </remarks>
        public static string ClustersFolder
        {
            get
            {
                var path = Path.Combine(GetNeonKubeUserFolder(), "clusters");

                Directory.CreateDirectory(path);

                return path;
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
                var path = Path.Combine(GetNeonKubeUserFolder(), "cache");

                Directory.CreateDirectory(path);

                return path;
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
        /// <param name="version">The optional component version.</param>
        /// <returns>The component file path.</returns>
        public static string GetComponentCachePath(KubeHostPlatform platform, string component, string version = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(component));

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
        /// Returns the path to the kubecontext extension file path for a specific context
        /// by raw name.
        /// </summary>
        /// <param name="rawName">The raw kubecontext name.</param>
        /// <returns>The file path.</returns>
        public static string GetContextExtensionPath(string rawName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(rawName));

            // Kubecontext names may include a forward slash to specify a Kubernetes
            // namespace.  This won't work for a file name, so we're going to replace
            // any of these 

            return Path.Combine(ClustersFolder, $"{rawName.Replace("/", "~")}.context.yaml");
        }

        /// <summary>
        /// Returns the path to the kubecontext extension file path for a specific context
        /// by structured name.
        /// </summary>
        /// <param name="name">The structured kubecontext name.</param>
        /// <returns>The file path.</returns>
        public static string GetContextExtensionPath(KubeConfigName name)
        {
            Covenant.Requires<ArgumentNullException>(name != null);

            return GetContextExtensionPath(name.ToString());
        }

        /// <summary>
        /// Returns the kubecontext extension for the structured configuration name.
        /// </summary>
        /// <param name="name">The structured context name.</param>
        /// <returns>The <see cref="KubeContextExtension"/> or <c>null</c>.</returns>
        public static KubeContextExtension GetContextExtension(KubeConfigName name)
        {
            Covenant.Requires<ArgumentNullException>(name != null);

            var path = GetContextExtensionPath(name.ToString());

            if (!File.Exists(path))
            {
                return null;
            }

            return NeonHelper.YamlDeserialize<KubeContextExtension>(File.ReadAllText(path));
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
                var path = Path.Combine(GetNeonKubeUserFolder(), "temp");

                Directory.CreateDirectory(path);

                return path;
            }
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
                {
                    var path = Path.Combine(GetNeonKubeUserFolder(), "vm-templates");

                    Directory.CreateDirectory(path);

                    return path;
                }
            }
        }

        /// <summary>
        /// Indicates whether the application is connected to thje cluster.
        /// </summary>
        public static bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Returns the user's current <see cref="KubeConfig"/>.
        /// </summary>
        public static KubeConfig KubeConfig
        {
            get
            {
                if (cachedKubeConfig != null)
                {
                    return cachedKubeConfig;
                }

                var configPath = KubeConfigPath;

                if (File.Exists(configPath))
                {
                    return cachedKubeConfig = NeonHelper.YamlDeserialize<KubeConfig>(File.ReadAllText(configPath));
                }

                return cachedKubeConfig = new KubeConfig();
            }
        }

        /// <summary>
        /// Returns the <see cref="KubeContext"/> for the connected cluster (or <c>null</c>).
        /// </summary>
        public static KubeConfigContext KubeContext
        {
            get
            {
                if (cachedKubeContext != null)
                {
                    return cachedKubeContext;
                }

                if (string.IsNullOrEmpty(KubeConfig.CurrentContext))
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
        /// This is used for special situations for setting up a cluster to
        /// set an uninitialized Kubernetes config context as the current
        /// <see cref="KubeContext"/>.
        /// </summary>
        /// <param name="context">The context being set or <c>null</c> to reset.</param>
        public static void SetKubeContext(KubeConfigContext context = null)
        {
            cachedKubeContext = context;
        }

        /// <summary>
        /// Returns the <see cref="Kube.ClusterProxy"/> for the current cluster (or <c>null</c>).
        /// </summary>
        public static ClusterProxy KubeProxy { get; private set; } = null;

        /// <summary>
        /// Looks for a certificate with a friendly name.
        /// </summary>
        /// <param name="store">The certificate store.</param>
        /// <param name="friendlyName">The case insensitive friendly name.</param>
        /// <returns>The certificate or <c>null</c> if one doesn't exist by the name.</returns>
        private static X509Certificate2 FindCertificateByFriendlyName(X509Store store, string friendlyName)
        {
            Covenant.Requires<ArgumentNullException>(store != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(friendlyName));

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
        /// Verifies that a cluster is connected.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a cluster is not connected.</exception>
        private static void VerifyConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("cluster is not connected.");
            }
        }
    }
}
