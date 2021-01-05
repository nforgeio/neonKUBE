//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Platform.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        private static bool             osChecked;
        private static string           osDescription;
        private static NetFramework?    netFramework = null;
        private static string           frameworkDescription;
        private static bool             isWindows;
        private static bool             isLinux;
        private static bool             isOSX;
        private static bool?            is64Bit;
        private static bool?            isDevWorkstation;
        private static bool?            isKubernetes;

        /// <summary>
        /// Detects the current operating system.
        /// </summary>
        private static void DetectOS()
        {
            if (osChecked)
            {
                return;     // Already did a detect
            }

            try
            {
                osDescription        = RuntimeInformation.OSDescription;
                frameworkDescription = RuntimeInformation.FrameworkDescription;
                isWindows            = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                isLinux              = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
                isOSX                = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
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
        public static string OsDescription
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
        /// Returns <c>true</c> if the application was built as 64-bit.
        /// </summary>
        public static bool Is64Bit
        {
            get
            {
                if (is64Bit.HasValue)
                {
                    return is64Bit.Value;
                }

                is64Bit = System.Runtime.InteropServices.Marshal.SizeOf<IntPtr>() == 8;

                return is64Bit.Value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the client was built as 32-bit.
        /// </summary>
        public static bool Is32BitBuild
        {
            get { return !Is64Bit; }
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
        /// Returns <c>true</c> if the current process is running on Max OSX.
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
    }
}
