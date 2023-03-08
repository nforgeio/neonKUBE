//-----------------------------------------------------------------------------
// FILE:	    GrpcDesktopService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Neon.Kube.DesktopService
{
    /// <summary>
    /// Implements the <see cref="IGrpcDesktopService"/>.
    /// </summary>
    public class GrpcDesktopService : IGrpcDesktopService
    {
        //---------------------------------------------------------------------
        // Static members

        private static HyperVClient     hyperv = new HyperVClient();

        /// <summary>
        /// Static constructor.
        /// </summary>
        public GrpcDesktopService()
        {
        }

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
        public async Task<GrpcFindNatReply> FindNatByNameAsync(GrpcFindNatByNameRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcFindNatReply(nat: hyperv.FindNatByName(request.Name).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcFindNatReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcFindNatReply> FindNatByNameSubnetAsync(GrpcFindNatBySubnetRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcFindNatReply(nat: hyperv.FindNatBySubnet(request.Subnet).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcFindNatReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetSwitchReply> FindSwitchAsync(GrpcGetSwitchRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcGetSwitchReply(@switch: hyperv.FindSwitch(request.SwitchName).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcGetSwitchReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetVmReply> FindVmAsync(GrpcGetVmRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcGetVmReply(machine: hyperv.FindVm(request.MachineName).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcGetVmReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpGetVmDrivesReply> ListVmDrivesAsync(GrpcGetVmDrivesRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpGetVmDrivesReply(hyperv.ListVmDrives(request.MachineName).ToList());
            }
            catch (Exception e)
            {
                return new GrpGetVmDrivesReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcGetVmNetworkAdaptersReply> ListVmNetworkAdaptersAsync(GrpcGetVmNetworkAdaptersRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                var adapters = hyperv.ListVmNetworkAdapters(machineName: request.MachineName, waitForAddresses: request.WaitForAddresses);

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
        public async Task<GrpcFindIPAddressReply> FindIPAddressAsync(GrpcFindIPAddressRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return new GrpcFindIPAddressReply(hyperv.FindIPAddress(request.Address).ToProto());
            }
            catch (Exception e)
            {
                return new GrpcFindIPAddressReply(e);
            }
        }

        /// <inheritdoc/>
        public async Task<GrpcRelayLogBatchReply> RelayLogBatchAsync(GrpcRelayLogBatchRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            // $todo(jefflill): Temporarily disabling this.
#if TODO
            var batch = NeonHelper.JsonDeserialize<Batch<LogRecord>>(request.BatchJson);

            DesktopService.LogExporter.Export(batch);
#endif
            return await Task.FromResult(new GrpcRelayLogBatchReply());
        }

        /// <inheritdoc/>
        public async Task<GrpcRelayTraceBatchReply> RelayTraceBatchAsync(GrpcRelayTraceBatchRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            var batch = NeonHelper.JsonDeserialize<Batch<Activity>>(request.BatchJson);

            // $todo(jefflill): Temporarily disabling this.
#if TODO
            DesktopService.TraceExporter.Export(batch);
#endif
            return await Task.FromResult(new GrpcRelayTraceBatchReply());
        }

        /// <inheritdoc/>
        public async Task<GrpcListLocalHostsSectionsReply> ListLocalHostSections(GrpcListLocalHostsSectionsRequest request, CallContext context = default)
        {
            await SyncContext.Clear;

            try
            {
                return await Task.FromResult(new GrpcListLocalHostsSectionsReply(NetHelper.ListLocalHostsSections()));
            }
            catch
            {
                return new GrpcListLocalHostsSectionsReply();
            }
        }
    }
}
