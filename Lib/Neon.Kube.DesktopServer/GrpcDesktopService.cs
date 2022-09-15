//-----------------------------------------------------------------------------
// FILE:	    GrpcDesktopService.cs
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
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Server;

using Neon.Common;
using Neon.HyperV;
using Neon.Kube.GrpcProto.Desktop;
using Neon.Net;
using Neon.Tasks;

namespace Neon.Kube.DesktopServer
{
    /// <summary>
    /// Implements the <see cref="IGrpcDesktopService"/>.
    /// </summary>
    public class GrpcDesktopService : IGrpcDesktopService
    {
        //---------------------------------------------------------------------
        // Static members

        // We're going to use the same client instance for all requests

        private static HyperVClient     hyperv = new HyperVClient();

        //---------------------------------------------------------------------
        // Instance members

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> AddVmAsync(GrpcAddVmRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                var extraDrives = new List<VirtualDrive>();

                if (request.ExtraDrives != null)
                {
                    foreach (var grpcDrive in request.ExtraDrives)
                    {
                        extraDrives.Add(grpcDrive.ToLocal());
                    }
                }

                hyperv.AddVm(
                    machineName:       request.MachineName,
                    memorySize:        request.MemorySize,
                    processorCount:    request.ProcessorCount ?? 4,
                    driveSize:         request.DriveSize,
                    drivePath:         request.DrivePath,
                    checkpointDrives:  request.CheckpointDrives,
                    templateDrivePath: request.TemplateDrivePath,
                    switchName:        request.SwitchName,
                    extraDrives:       extraDrives);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> AddVmDriveAsync(GrpcAddVmDriveRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.AddVmDrive(
                    machineName: request.MachineName,
                    drive:       request.Drive.ToLocal());

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> CompactDriveRequestAsync(GrpcCompactDriveRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.CompactDrive(drivePath: request.DrivePath);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> EjectVmDvdAsync(GrpcEjectVmDvdRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.EjectVmDvd(machineName: request.MachineName);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetNatReply> GetNatByName(GrpcGetNatByNameRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcGetNatReply(nat: hyperv.GetNatByName(request.Name).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcGetNatReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetNatReply> GetNatByNameSubnet(GrpcGetNatBySubnetRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcGetNatReply(nat: hyperv.GetNatBySubnet(request.Subnet).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcGetNatReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetSwitchReply> GetSwitchAsync(GrpcGetSwitchRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcGetSwitchReply(@switch: hyperv.GetSwitch(request.SwitchName).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcGetSwitchReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetVmReply> GetVmAsync(GrpcGetVmRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcGetVmReply(machine: hyperv.GetVm(request.MachineName).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcGetVmReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpGetVmDrivesReply> GetVmDrivesAsync(GrpcGetVmDrivesRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpGetVmDrivesReply(hyperv.GetVmDrives(request.MachineName));
            }
            catch (Exception e)
            {
                return new GrpGetVmDrivesReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetVmNetworkAdaptersReply> GetVmNetworkAdaptersAsync(GrpcGetVmNetworkAdaptersRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                var adapters = hyperv.GetVmNetworkAdapters(machineName: request.MachineName, waitForAddresses: request.WaitForAddresses);

                return new GrpcGetVmNetworkAdaptersReply(adapters: adapters.Select(adapter => adapter.ToProto()).ToList());
            }
            catch (Exception e)
            {
                return new GrpcGetVmNetworkAdaptersReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetWindowsOptionalFeaturesReply> GetWindowsOptionalFeaturesAsync(GrpcGetWindowsOptionalFeaturesRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcGetWindowsOptionalFeaturesReply(NeonHelper.GetWindowsOptionalFeatures());
            }
            catch (Exception e)
            {
                return new GrpcGetWindowsOptionalFeaturesReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> InsertVmDvdAsync(GrpcInsertVmDvdRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.InsertVmDvd(machineName: request.MachineName, isoPath: request.IsoPath);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcIsNestedVirtualizationReply> IsNestedVirtualizationAsync(GrpcIsNestedVirtualizationRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcIsNestedVirtualizationReply(isNested: hyperv.IsNestedVirtualization);
            }
            catch (Exception e)
            {
                return new GrpcIsNestedVirtualizationReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcListNatsReply> ListNatsAsync(GrpcListNatsRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcListNatsReply(nats: hyperv.ListNats().Select(nat => nat.ToProto()).ToList());
            }
            catch (Exception e)
            {
                return new GrpcListNatsReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcListSwitchesReply> ListSwitchesAsync(GrpcListSwitchesRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcListSwitchesReply(switches: hyperv.ListSwitches().Select(@switch => @switch.ToProto()).ToList());
            }
            catch (Exception e)
            {
                return new GrpcListSwitchesReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcListVmsReply> ListVmsAsync(GrpcListVmsRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcListVmsReply(virtualMachines: hyperv.ListVms().Select(vm => vm.ToProto()).ToList());
            }
            catch (Exception e)
            {
                return new GrpcListVmsReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> NewExternalSwitchAsync(GrpcNewExternalSwitchRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.NewExternalSwitch(switchName: request.SwitchName, IPAddress.Parse(request.Gateway));

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> NewInternalSwitchAsync(GrpcNewInternalSwitchRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.NewInternalSwitch(
                    switchName: request.SwitchName,
                    subnet:     NetworkCidr.Parse(request.Subnet),
                    addNat:     request.AddNat);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> RemoveSwitchAsync(GrpcRemoveSwitchRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.RemoveSwitch(switchName: request.SwitchName, ignoreMissing: request.IgnoreMissing ?? false);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> RemoveVmAsync(GrpcRemoveVmRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.RemoveVm(machineName: request.MachineName);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> SaveVmAsync(GrpcSaveVmRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.SaveVm(machineName: request.MachineName);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> StartVmAsync(GrpcStartVmRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.StartVm(machineName: request.MachineName);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcBaseReply> StopVmAsync(GrpcStopVmRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                hyperv.StopVm(machineName: request.MachineName, turnOff: request.TurnOff);

                return new GrpcBaseReply();
            }
            catch (Exception e)
            {
                return new GrpcBaseReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcVmExistsReply> VmExistsAsync(GrpcVmExistsRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcVmExistsReply(hyperv.VmExists(request.MachineName));
            }
            catch (Exception e)
            {
                return new GrpcVmExistsReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetIPAddressReply> GetIPAddress(GrpcGetIPAddressRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcGetIPAddressReply(hyperv.GetIPAddress(request.Address).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcGetIPAddressReply(e);
            }
        }

        /// <inheritdoc/>
        public Task<GrpcTelemetryLogReply> ForwardTelemetryLogs(GrpcTelemetryLogRequest request, CallContext context = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<GrpcTelemetryTraceReply> ForwardTelemetryTraces(GrpcTelemetryTraceRequest request, CallContext context = default)
        {
            throw new NotImplementedException();
        }
    }
}
