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

namespace Neon.Kube
{
    /// <summary>
    /// cluster related utilties.
    /// </summary>
    public static partial class KubeHelper
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Low-level Windows APIs.
        /// </summary>
        private static class Windows
        {
            [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EncryptFile(string filename);
        }

        //---------------------------------------------------------------------
        // Implementation

        private static INeonLogger  log = LogManager.Default.GetLogger(typeof(KubeHelper));

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
                return Windows.EncryptFile(path);
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
                return Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".neonkube");
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
        /// Returns the path the folder containing login information for the known logins, creating
        /// the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// <para>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage neonKUBEs.  Each known cluster will have a JSON file named
        /// <b><i>hive-name</i>.json</b> holding the serialized <see cref="Kube.KubeConfig"/> 
        /// information for the cluster.
        /// </para>
        /// <para>
        /// The <b>.current</b> file (if present) specifies the name of the cluster to be considered
        /// to be currently logged in.
        /// </para>
        /// </remarks>
        public static string LoginFolder
        {
            get
            {
                var path = Path.Combine(GetNeonKubeUserFolder(), "logins");

                Directory.CreateDirectory(path);

                return path;
            }
        }

        /// <summary>
        /// Returns the path the neonFORGE temporary folder, creating the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage neonKUBEs.  The client will use this to store temporary files that may
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
        /// Returns the path to the root folder containing the installed Ansible role files.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string AnsibleRolesFolder
        {
            get
            {
                var path = Path.Combine(GetNeonKubeUserFolder(), "ansible", "roles");

                Directory.CreateDirectory(path);

                return path;
            }
        }

        /// <summary>
        /// Returns the path to the root folder containing the Ansible Vault password files.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string AnsiblePasswordsFolder
        {
            get
            {
                var path = Path.Combine(GetNeonKubeUserFolder(), "ansible", "passwords");

                Directory.CreateDirectory(path);

                return path;
            }
        }

        /// <summary>
        /// Returns the path to the file indicating which cluster is currently logged in.
        /// </summary>
        public static string CurrentPath
        {
            get { return Path.Combine(LoginFolder, ".current"); }
        }

        /// <summary>
        /// Returns the path to the login information for the named cluster.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="hiveName">The cluster name.</param>
        /// <returns>The path to the cluster's credentials file.</returns>
        public static string GetLoginPath(string username, string hiveName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));

            return Path.Combine(LoginFolder, $"{username}@{hiveName}.login.json");
        }

        /// <summary>
        /// Returns the path to the current user's cluster virtual machine templates
        /// folder, creating the directory if it doesn't already exist.
        /// </summary>
        /// <returns>The path to the cluster setup folder.</returns>
        public static string GetVmTemplatesFolder()
        {
            var path = Path.Combine(GetNeonKubeUserFolder(), "vm-templates");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Indicates whether the application is connected to thje cluster.
        /// </summary>
        public static bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Returns the <see cref="Kube.KubeConfig"/> for the opened cluster. 
        /// </summary>
        public static KubeConfig KubeContext { get; private set; } = null;

        /// <summary>
        /// Returns the <see cref="Kube.KubeProxy"/> for the opened cluster.
        /// </summary>
        public static KubeProxy KubeProxy { get; private set; } = null;

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

        /// <summary>
        /// Determines whether a hostname refers to the Docker public registry.
        /// </summary>
        /// <param name="hostname">The hostname being tested.</param>
        /// <returns>><c>true</c> for the public registry.</returns>
        public static bool IsDockerPublicRegistry(string hostname)
        {
            if (hostname == null)
            {
                return false;
            }

            return hostname.Equals(KubeConst.DockerPublicRegistry, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
