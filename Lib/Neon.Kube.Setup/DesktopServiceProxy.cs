//-----------------------------------------------------------------------------
// FILE:        DesktopServiceProxy.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

using Grpc.Net.Client;

using Neon.Common;
using Neon.HyperV;
using Neon.Kube.GrpcProto;
using Neon.Kube.GrpcProto.Desktop;
using Neon.Net;
using Neon.Tasks;

using ProtoBuf.Grpc.Client;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Used to proxy non-Hyper-V operations to the Neon Desktop service or
    /// execute them directly when the current process is running with 
    /// elevated privileges.
    /// </summary>
    public sealed class DesktopServiceProxy : IDisposable
    {
        private bool                    isDisposed = false;
        private bool                    isAdmin;
        private GrpcChannel             desktopServiceChannel;
        private IGrpcDesktopService     desktopService;
        private HyperVClient            hyperV;

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

                if (desktopServiceChannel == null)
                {
                    throw new NeonKubeException("[neon-desktop-service] is not running.");
                }

                desktopService = desktopServiceChannel.CreateGrpcService<IGrpcDesktopService>();
            }
            else
            {
                hyperV = new HyperVClient();
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
            await SyncContext.Clear;

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

        /// <summary>
        /// Checks if the named VM exists.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<bool> VmExistsAsync(string name)
        {
            await SyncContext.Clear;

            if (isAdmin)
            {
                return hyperV.ListVms().Any(vm => vm.Name == name);
            }
            else
            {
                return desktopService.VmExistsAsync(new GrpcVmExistsRequest()
                {
                    MachineName = KubeConst.NeonDesktopClusterName
                }).GetAwaiter().GetResult().Exists;
            }
        }
    }
}
