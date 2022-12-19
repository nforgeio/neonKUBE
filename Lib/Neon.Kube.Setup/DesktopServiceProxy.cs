//-----------------------------------------------------------------------------
// FILE:	    DesktopServiceProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Grpc.Net.Client;

using k8s;
using k8s.Models;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.GrpcProto;
using Neon.Kube.GrpcProto.Desktop;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

using ProtoBuf.Grpc.Client;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Used to proxy non-HyperV operations to the Neon Desktop service or
    /// execute them directly when the current process is running with 
    /// elevated privileges.
    /// </summary>
    public sealed class DesktopServiceProxy : IDisposable
    {
        private bool                    isDisposed = false;
        private bool                    isAdmin;
        private GrpcChannel             desktopServiceChannel;
        private IGrpcDesktopService     desktopService;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="isAdminOverride">
        /// Optionally overrides detection of elevated permissions enabled for the 
        /// current process.  This is used for testing.
        /// </param>
        /// <param name="socketPath">
        /// Optionally overrides the default desktop service unix socket path.  This
        /// is used for testing purposes.  This defaults to <see cref="KubeHelper.WinDesktopServiceSocketPath"/>
        /// where <b>neon-desktop</b> and <b>neon-cli</b> expect it to be.
        /// </param>
        public DesktopServiceProxy(bool? isAdminOverride = null, string socketPath = null)
        {
            if (isAdminOverride.HasValue)
            {
                isAdmin = isAdminOverride.Value;
            }
            else
            {
                isAdmin = NeonHelper.HasElevatedPermissions;
            }

            if (!isAdmin)
            {
                desktopServiceChannel = NeonGrpcServices.CreateDesktopServiceChannel(socketPath);
                desktopService        = desktopServiceChannel.CreateGrpcService<IGrpcDesktopService>();
            }
        }

        /// <summary>
        /// Alternate constructor the associated an open gRPC channel with the instance.
        /// </summary>
        /// <param name="channel">The open gRPC channel.</param>
        public DesktopServiceProxy(GrpcChannel channel)
        {
            Covenant.Requires<ArgumentNullException>(channel != null, nameof(channel));

            isAdmin = NeonHelper.HasElevatedPermissions;

            if (!isAdmin)
            {
                desktopServiceChannel = channel;
                desktopService        = desktopServiceChannel.CreateGrpcService<IGrpcDesktopService>();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            if (!isAdmin)
            {
                desktopServiceChannel.Dispose();

                desktopServiceChannel = null;
                desktopService        = null;
            }
        }

        /// <summary>
        /// <para>
        /// Used to modify the <b>$/etc/hosts</b> file used by the DNS resolver for testing, debugging 
        /// and possibly other purposes.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> Modifying the <b>hosts</b> file will impact all processes
        /// on the system, not just the current process.
        /// </note>
        /// </summary>
        /// <param name="section">
        /// <para>
        /// Specifies the string to use to delimit the host names section.  This is required and
        /// must be a non-empty string consisting of up to 63 non-control ASCII characters.  Section
        /// names are case sensitive.
        /// </para>
        /// </param>
        /// <param name="hostEntries">A dictionary mapping the hostnames to an IP address or <c>null</c>.</param>
        /// <remarks>
        /// <note>
        /// This method requires elevated administrative privileges.
        /// </note>
        /// <para>
        /// This method adds or removes a temporary section of host entry definitions
        /// delimited by special comment lines.  When <paramref name="hostEntries"/> is 
        /// non-null and non-empty, the section will be added or updated.  Otherwise, the
        /// section will be removed.
        /// </para>
        /// <para>
        /// You can remove all host sections by passing both <paramref name="hostEntries"/> 
        /// and <paramref name="section"/> as <c>null</c>.
        /// </para>
        /// </remarks>
        public void ModifyLocalHosts(string section, Dictionary<string, IPAddress> hostEntries = null)
        {
            if (isAdmin)
            {
                NetHelper.ModifyLocalHosts(section, hostEntries);
            }
            else
            {
                var request = new GrpcModifyLocalHostsRequest(section, hostEntries);
                var reply   = desktopService.ModifyLocalHosts(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Lists the names of the local host sections.
        /// </summary>
        /// <returns>The section names converted to uppercase.</returns>
        public IEnumerable<LocalHostSection> ListLocalHostsSections()
        {
            if (isAdmin)
            {
                return NetHelper.ListLocalHostsSections();
            }
            else
            {
                var request = new GrpcListLocalHostsSectionsRequest();
                var reply   = desktopService.ListLocalHostSections(request).Result;

                return reply.Sections.Select(section => section.ToLocalHostSection());
            }
        }

        /// <summary>
        /// Returns the status of optional Windows features.
        /// </summary>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> mapping feature names to <see cref="WindowsFeatureStatus"/>"/> instances.</returns>
        public async Task<Dictionary<string, WindowsFeatureStatus>> GetWindowsOptionalFeaturesAsync()
        {
            if (isAdmin)
            {
                return NeonHelper.GetWindowsOptionalFeatures();
            }
            else
            {
                var request = new GrpcGetWindowsOptionalFeaturesRequest();
                var reply   = await desktopService.GetWindowsOptionalFeaturesAsync(request);

                return reply.Capabilities;
            }
        }
    }
}
