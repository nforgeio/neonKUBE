//-----------------------------------------------------------------------------
// FILE:	    IGrpcDesktopService.cs
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
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

using ProtoBuf.Grpc;

namespace Neon.Kube.GrpcProto.Desktop
{
    /// <summary>
    /// Defines the Neon Desktop Service contract.  This is used by <b>neon-desktop</b> and <b>neon-cli</b>
    /// to perform privileged operations.
    /// </summary>
    [ServiceContract]
    public interface IGrpcDesktopService
    {
        /// <summary>
        /// Returns a dictionary mapping Windows features to their current status.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcGetWindowsOptionalFeaturesReply"/> holding the feature information.</returns>
        /// <exception cref="GrpcServiceException">Thrown on errors.</exception>
        [OperationContract]
        Task<GrpcGetWindowsOptionalFeaturesReply> GetWindowsOptionalFeaturesAsync(GrpcGetWindowsOptionalFeaturesRequest request, CallContext context = default);

        /// <summary>
        /// Returns an indication as to whether Windows is running with nested virtualization.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcIsNestedVirtualizationReply"/> holding the feature information.</returns>
        /// <exception cref="GrpcServiceException">Thrown on errors.</exception>
        [OperationContract]
        Task<GrpcIsNestedVirtualizationReply> IsNestedVirtualizationAsync(GrpcIsNestedVirtualizationRequest request, CallContext context = default);

        /// <summary>
        /// Creates a virtual machine. 
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        [OperationContract]
        Task<GrpcBaseReply> AddVmAsync(GrpcAddVmRequest request, CallContext context = default);

        /// <summary>
        /// Removes a named virtual machine and all of its drives (by default).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/> indicating success or failure.</returns>
        [OperationContract]
        Task<GrpcBaseReply> RemoveVmAsync(GrpcRemoveVmRequest request, CallContext context = default);

        /// <summary>
        /// Lists the Hyper-V virtual machines.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcListVmsReply"/> with the results.</returns>
        [OperationContract]
        Task<GrpcListVmsReply> ListVmsAsync(GrpcListVmsRequest request, CallContext context = default);

        /// <summary>
        /// Returns information about a specific virtual machine if it exists.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcGetVmReply"/> with the results.</returns>
        [OperationContract]
        Task<GrpcGetVmReply> GetVmAsync(GrpcGetVmRequest request, CallContext context = default);

        /// <summary>
        /// Determines whether a virtual machine exists.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcVmExistsReply"/> with the result.</returns>
        [OperationContract]
        Task<GrpcVmExistsReply> VmExistsAsync(GrpcVmExistsRequest request, CallContext context = default);

        /// <summary>
        /// Starts a virtual machine.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> StartVmAsync(GrpcStartVmRequest request, CallContext context = default);

        /// <summary>
        /// Stops a virtual machine.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> StopVmAsync(GrpcStopVmRequest request, CallContext context = default);

        /// <summary>
        /// Saves a virtual machine (AKA puts it to sleep).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> SaveVmAsync(GrpcSaveVmRequest request, CallContext context = default);

        /// <summary>
        /// Returns information about a virtual machine's attached drives.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpGetVmDrivesReply"/>.</returns>
        [OperationContract]
        Task<GrpGetVmDrivesReply> GetVmDrivesAsync(GrpcGetVmDrivesRequest request, CallContext context = default);

        /// <summary>
        /// Adds a drive to a virtual machine.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> AddVmDriveAsync(GrpcAddVmDriveRequest request, CallContext context = default);

        /// <summary>
        /// Compacts a virtual disk.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> CompactDriveRequestAsync(GrpcCompactDriveRequest request, CallContext context = default);

        /// <summary>
        /// Inserts an ISO file as the DVD/CD for a virtual machine, ejecting any existing disc.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> InsertVmDvdAsync(GrpcInsertVmDvdRequest request, CallContext context = default);

        /// <summary>
        /// Ejects any DVD/CD that's currently inserted into a virtual machine.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> EjectVmDvdAsync(GrpcEjectVmDvdRequest request, CallContext context = default);

        /// <summary>
        /// Lists the Hyper-V virtual switches.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcListSwitchesReply"/>.</returns>
        [OperationContract]
        Task<GrpcListSwitchesReply> ListSwitchesAsync(GrpcListSwitchesRequest request, CallContext context = default);

        /// <summary>
        /// Returns information about a specific Hyper-V virtual switch.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcGetSwitchReply"/>.</returns>
        [OperationContract]
        Task<GrpcGetSwitchReply> GetSwitchAsync(GrpcGetSwitchRequest request, CallContext context = default);

        /// <summary>
        /// Creates a new external Hyper-V virtual switch.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> NewExternalSwitchAsync(GrpcNewExternalSwitchRequest request, CallContext context = default);

        /// <summary>
        /// Creates a new internal Hyper-V virtual switch.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> NewInternalSwitchAsync(GrpcNewInternalSwitchRequest request, CallContext context = default);

        /// <summary>
        /// Removes a Hyper-V virtual switch.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> RemoveSwitchAsync(GrpcRemoveSwitchRequest request, CallContext context = default);

        /// <summary>
        /// Returns information about the network adaptors attached to a virtual machine.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcGetVmNetworkAdaptersReply"/>.</returns>
        [OperationContract]
        Task<GrpcGetVmNetworkAdaptersReply> GetVmNetworkAdaptersAsync(GrpcGetVmNetworkAdaptersRequest request, CallContext context = default);

        /// <summary>
        /// Lists the virtual Hyper-V NATs.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcListNatsReply"/>.</returns>
        [OperationContract]
        Task<GrpcListNatsReply> ListNatsAsync(GrpcListNatsRequest request, CallContext context = default);

        /// <summary>
        /// Looks up a virtual Hyper-V NAT by name.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcGetNatReply"/>.</returns>
        [OperationContract]
        Task<GrpcGetNatReply> GetNatByNameAsync(GrpcGetNatByNameRequest request, CallContext context = default);

        /// <summary>
        /// Looks up a virtual Hyper-V NAT by subnet.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcGetNatReply"/>.</returns>
        [OperationContract]
        Task<GrpcGetNatReply> GetNatByNameSubnetAsync(GrpcGetNatBySubnetRequest request, CallContext context = default);

        /// <summary>
        /// Returns information about a virtual IP address.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>A <see cref="GrpcGetIPAddressReply"/>.</returns>
        [OperationContract]
        Task<GrpcGetIPAddressReply> GetIPAddressAsync(GrpcGetIPAddressRequest request, CallContext context = default);

        /// <summary>
        /// Sends a batch of telemetry logs to the <b>neon-desktop-service</b> which will then forward them on to the headend.
        /// </summary>
        /// <param name="request">The request holding the batch of log records.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>The <see cref="GrpcRelayLogBatchReply"/>.</returns>
        [OperationContract]
        Task<GrpcRelayLogBatchReply> RelayLogBatchAsync(GrpcRelayLogBatchRequest request, CallContext context = default);

        /// <summary>
        /// Sends a batch of telemetry traces to the <b>neon-desktop-service</b> which will then forward them on to the headend.
        /// </summary>
        /// <param name="request">The request holding the batch of traces.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>The <see cref="GrpcRelayTraceBatchReply"/>.</returns>
        [OperationContract]
        Task<GrpcRelayTraceBatchReply> RelayTraceBatchAsync(GrpcRelayTraceBatchRequest request, CallContext context = default);

        /// <summary>
        /// Modifies the local <b>$/etc/hosts</b> file which usually required elevated rights to access.
        /// </summary>
        /// <param name="request">The request specifying how to modify the local hosts.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>The <see cref="GrpcBaseReply"/>.</returns>
        [OperationContract]
        Task<GrpcBaseReply> ModifyLocalHosts(GrpcModifyLocalHostsRequest request, CallContext context = default);

        /// <summary>
        /// Lists the sections from the local <b>$/etc/hosts</b> file.
        /// </summary>
        /// <param name="request">The request specifying how to modify the local hosts.</param>
        /// <param name="context">Optionally specifies the gRPC call context.</param>
        /// <returns>The <see cref="GrpcListLocalHostsSectionsReply"/>.</returns>
        [OperationContract]
        Task<GrpcListLocalHostsSectionsReply> ListLocalHostSections(GrpcListLocalHostsSectionsRequest request, CallContext context = default);
    }
}
