//-----------------------------------------------------------------------------
// FILE:	    HyperVProxy.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.HyperV;
using Neon.IO;
using Neon.Kube.GrpcProto;
using Neon.Kube.GrpcProto.Desktop;
using Neon.Net;
using Neon.SSH;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Handles Hyper-V operations by calling a <see cref="HyperVClient"/> directly when the current process
    /// is running with elevated permissions or calling the <b>Neon Desktop Service</b> via gRPC when the
    /// current process doesn't have these rights.
    /// </para>
    /// <para>
    /// The <b>Neon Desktop Service</b> is installed along with <b>neon-desktop</b> and <b>neon-cli</b> and
    /// runs as a Windows Service as administrator.
    /// </para>
    /// </summary>
    internal sealed class HyperVProxy : IDisposable
    {
        private bool                    isDisposed = false;
        private bool                    isAdmin;
        private HyperVClient            hypervClient;
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
        public HyperVProxy(bool? isAdminOverride = null, string socketPath = null)
        {
            if (isAdminOverride.HasValue)
            {
                isAdmin = isAdminOverride.Value;
            }
            else
            {
                isAdmin = NeonHelper.HasElevatedPermissions;
            }

            if (isAdmin)
            {
                hypervClient = new HyperVClient();
            }
            else
            {
                desktopServiceChannel = NeonGrpcServices.CreateDesktopServiceChannel(socketPath);
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

            if (isAdmin)
            {
                hypervClient.Dispose();
                hypervClient = null;
            }
            else
            {
                desktopServiceChannel.Dispose();

                desktopServiceChannel    = null;
                desktopService = null;
            }
        }

        /// <summary>
        /// Returns the current status of optional Windows features.  
        /// </summary>
        /// <returns>A dictionary mapping individual feature names to their status.</returns>
        public Dictionary<string, WindowsFeatureStatus> GetWindowsOptionalFeatures()
        {
            if (isAdmin)
            {
                return NeonHelper.GetWindowsOptionalFeatures();
            }
            else
            {
                var request = new GrpcGetWindowsOptionalFeaturesRequest();
                var reply   = desktopService.GetWindowsOptionalFeaturesAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Capabilities;
            }
        }

        /// <summary>
        /// Determines whether the current machine is already running as a Hyper-V
        /// virtual machine and that any Hyper-V VMs deployed on this machine can
        /// be considered to be nested.
        /// </summary>
        /// <remarks>
        /// <para>
        /// We use the presence of this registry value to detect VM nesting:
        /// </para>
        /// <example>
        /// HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Virtual Machine\Auto\OSName
        /// </example>
        /// </remarks>
        public bool IsNestedVirtualization
        {
            get
            {
                if (isAdmin)
                {
                    return hypervClient.IsNestedVirtualization;
                }
                else
                {
                    var request = new GrpcIsNestedVirtualizationRequest();
                    var reply   = desktopService.IsNestedVirtualizationAsync(request).Result;

                    reply.Error.EnsureSuccess();

                    return reply.IsNested;
                }
            }
        }

        /// <summary>
        /// Creates a virtual machine. 
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <param name="memorySize">
        /// A string specifying the memory size.  This can be a long byte count or a
        /// byte count or a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2GiB</b>, 
        /// or <b>1TiB</b>.  This defaults to <b>2GiB</b>.
        /// </param>
        /// <param name="processorCount">
        /// The number of virutal processors to assign to the machine.  This defaults to <b>4</b>.
        /// </param>
        /// <param name="driveSize">
        /// A string specifying the primary disk size.  This can be a long byte count or a
        /// byte count or a number with units like <b>512MB</b>, <b>0.5GiB</b>, <b>2GiB</b>, 
        /// or <b>1TiB</b>.  Pass <c>null</c> to leave the disk alone.  This defaults to <c>null</c>.
        /// </param>
        /// <param name="drivePath">
        /// Optionally specifies the path where the virtual hard drive will be located.  Pass 
        /// <c>null</c> or empty to default to <b>MACHINE-NAME.vhdx</b> located in the default
        /// Hyper-V virtual machine drive folder.
        /// </param>
        /// <param name="checkpointDrives">Optionally enables drive checkpoints.  This defaults to <c>false</c>.</param>
        /// <param name="templateDrivePath">
        /// If this is specified and <paramref name="drivePath"/> is not <c>null</c> then
        /// the hard drive template at <paramref name="templateDrivePath"/> will be copied
        /// to <paramref name="drivePath"/> before creating the machine.
        /// </param>
        /// <param name="switchName">Optional name of the virtual switch.</param>
        /// <param name="extraDrives">
        /// Optionally specifies any additional virtual drives to be created and 
        /// then attached to the new virtual machine.
        /// </param>
        /// <remarks>
        /// <note>
        /// The <see cref="VirtualDrive.Path"/> property of <paramref name="extraDrives"/> may be
        /// passed as <c>null</c> or empty.  In this case, the drive name will default to
        /// being located in the standard Hyper-V virtual drivers folder and will be named
        /// <b>MACHINE-NAME-#.vhdx</b>, where <b>#</b> is the one-based index of the drive
        /// in the enumeration.
        /// </note>
        /// </remarks>
        public void AddVm(
            string                      machineName, 
            string                      memorySize        = "2GiB", 
            int                         processorCount    = 4,
            string                      driveSize         = null,
            string                      drivePath         = null,
            bool                        checkpointDrives  = false,
            string                      templateDrivePath = null, 
            string                      switchName        = null,
            IEnumerable<VirtualDrive>   extraDrives       = null)
        {
            if (isAdmin)
            {
                hypervClient.AddVm(
                    machineName:       machineName,
                    memorySize:        memorySize,
                    processorCount:    processorCount,
                    driveSize:         driveSize,
                    drivePath:         drivePath,
                    checkpointDrives:  checkpointDrives,
                    templateDrivePath: templateDrivePath,
                    switchName:        switchName,
                    extraDrives:       extraDrives);
            }
            else
            {
                var request = new GrpcAddVmRequest(
                    machineName:       machineName,
                    memorySize:        memorySize,
                    processorCount:    processorCount,
                    driveSize:         driveSize,
                    drivePath:         drivePath,
                    checkpointDrives:  checkpointDrives,
                    templateDrivePath: templateDrivePath,
                    switchName:        switchName,
                    extraDrives:       extraDrives?.Select(drive => drive.ToProto()));

                desktopService.AddVmAsync(request).Result.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Removes a named virtual machine and all of its drives (by default).
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <param name="keepDrives">Optionally retains the VM disk files.</param>
        public void RemoveVm(string machineName, bool keepDrives = false)
        {
            if (isAdmin)
            {
                hypervClient.RemoveVm(machineName: machineName, keepDrives: keepDrives);
            }
            else
            {
                var request = new GrpcRemoveVmRequest(machineName: machineName, keepDrives: keepDrives);

                desktopService.RemoveVmAsync(request).Result.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Lists the virtual machines.
        /// </summary>
        /// <returns><see cref="IEnumerable{VirtualMachine}"/>.</returns>
        public IEnumerable<VirtualMachine> ListVms()
        {
            if (isAdmin)
            {
                return hypervClient.ListVms();
            }
            else
            {
                var request = new GrpcListVmsRequest();
                var reply   = desktopService.ListVmsAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.VirtualMachines.Select(vm => vm.ToLocal());
            }
        }

        /// <summary>
        /// Gets the current status for a named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <returns>The <see cref="VirtualMachine"/> or <c>null</c> when the virtual machine doesn't exist..</returns>
        public VirtualMachine GetVm(string machineName)
        {
            if (isAdmin)
            {
                return hypervClient.GetVm(machineName: machineName);
            }
            else
            {
                var request = new GrpcGetVmRequest(machineName: machineName);
                var reply   = desktopService.GetVmAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Machine.ToLocal();
            }
        }

        /// <summary>
        /// Determines whether a named virtual machine exists.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <returns><c>true</c> if the machine exists.</returns>
        public bool VmExists(string machineName)
        {
            if (isAdmin)
            {
                return hypervClient.VmExists(machineName: machineName);
            }
            else
            {
                var request = new GrpcVmExistsRequest(machineName: machineName);
                var reply   = desktopService.VmExistsAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Exists;
            }
        }

        /// <summary>
        /// Starts the named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        public void StartVm(string machineName)
        {
            if (isAdmin)
            {
                hypervClient.StartVm(machineName: machineName);
            }
            else
            {
                var request = new GrpcStartVmRequest(machineName: machineName);
                var reply   = desktopService.StartVmAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Stops the named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <param name="turnOff">
        /// <para>
        /// Optionally just turns the VM off without performing a graceful shutdown first.
        /// </para>
        /// <note>
        /// <b>WARNING!</b> This could result in corruption or the the loss of unsaved data.
        /// </note>
        /// </param>
        public void StopVm(string machineName, bool turnOff = false)
        {
            if (isAdmin)
            {
                hypervClient.StopVm(machineName: machineName, turnOff: turnOff);
            }
            else
            {
                var request = new GrpcStopVmRequest(machineName: machineName, turnOff: turnOff);
                var reply   = desktopService.StopVmAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Persists the state of a running virtual machine and then stops it.  This is 
        /// equivalent to hibernation for a physical machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        public void SaveVm(string machineName)
        {
            if (isAdmin)
            {
                hypervClient.SaveVm(machineName: machineName);
            }
            else
            {
                var request = new GrpcSaveVmRequest(machineName: machineName);
                var reply   = desktopService.SaveVmAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Returns host file system paths to any virtual drives attached to
        /// the named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <returns>The list of fully qualified virtual drive file paths.</returns>
        public List<string> GetVmDrives(string machineName)
        {
            if (isAdmin)
            {
                return hypervClient.GetVmDrives(machineName: machineName);
            }
            else
            {
                var request = new GrpcGetVmDrivesRequest(machineName: machineName);
                var reply   = desktopService.GetVmDrivesAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.DrivePaths;
            }
        }

        /// <summary>
        /// Creates a new virtual drive and adds it to a virtual machine.
        /// </summary>
        /// <param name="machineName">The target virtual machine name.</param>
        /// <param name="drive">The new drive information.</param>
        public void AddVmDrive(string machineName, VirtualDrive drive)
        {
            if (isAdmin)
            {
                hypervClient.AddVmDrive(machineName: machineName, drive: drive);
            }
            else
            {
                var request = new GrpcAddVmDriveRequest(machineName: machineName, drive: drive.ToProto());
                var reply   = desktopService.AddVmDriveAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// <para>
        /// Compacts a dynamic VHD or VHDX virtual disk file.
        /// </para>
        /// <note>
        /// The disk may be mounted to a VM but the VM cannot be running.
        /// </note>
        /// </summary>
        /// <param name="drivePath">Path to the virtual drive file.</param>
        public void CompactDrive(string drivePath)
        {
            if (isAdmin)
            {
                hypervClient.CompactDrive(drivePath: drivePath);
            }
            else
            {
                var request = new GrpcCompactDriveRequest(drivePath: drivePath);
                var reply   = desktopService.CompactDriveRequestAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Inserts an ISO file as the DVD/CD for a virtual machine, ejecting any
        /// existing disc.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <param name="isoPath">Path to the ISO file.</param>
        public void InsertVmDvd(string machineName, string isoPath)
        {
            if (isAdmin)
            {
                hypervClient.InsertVmDvd(machineName: machineName, isoPath: isoPath);
            }
            else
            {
                var request = new GrpcInsertVmDvdRequest(machineName: machineName, isoPath: isoPath);
                var reply   = desktopService.InsertVmDvdAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Ejects any DVD/CD that's currently inserted into a virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        public void EjectVmDvd(string machineName)
        {
            if (isAdmin)
            {
                hypervClient.EjectVmDvd(machineName: machineName);
            }
            else
            {
                var request = new GrpcEjectVmDvdRequest(machineName: machineName);
                var reply   = desktopService.EjectVmDvdAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Returns the virtual network switches.
        /// </summary>
        /// <returns>The list of switches.</returns>
        public List<VirtualSwitch> ListSwitches()
        {
            if (isAdmin)
            {
                return hypervClient.ListSwitches();
            }
            else
            {
                var request = new GrpcListSwitchesRequest();
                var reply   = desktopService.ListSwitchesAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Switches.Select(@switch => @switch.ToLocal()).ToList();
            }
        }

        /// <summary>
        /// Returns information for a Hyper-V switch by name.
        /// </summary>
        /// <param name="switchName">The switch name.</param>
        /// <returns>The <see cref="VirtualSwitch"/> when present or <c>null</c>.</returns>
        public VirtualSwitch GetSwitch(string switchName)
        {
            if (isAdmin)
            {
                return hypervClient.GetSwitch(switchName: switchName);
            }
            else
            {
                var request = new GrpcGetSwitchRequest(switchName: switchName);
                var reply   = desktopService.GetSwitchAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Switch.ToLocal();
            }
        }

        /// <summary>
        /// Adds a virtual Hyper-V switch that has external connectivity.
        /// </summary>
        /// <param name="switchName">The new switch name.</param>
        /// <param name="gateway">Address of the LAN gateway, used to identify the connected network interface.</param>
        public void NewExternalSwitch(string switchName, IPAddress gateway)
        {
            if (isAdmin)
            {
                hypervClient.NewExternalSwitch(switchName: switchName, gateway: gateway);
            }
            else
            {
                var request = new GrpcNewExternalSwitchRequest(switchName: switchName, gateway: gateway);
                var reply   = desktopService.NewExternalSwitchAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Adds an internal Hyper-V switch configured for the specified subnet and gateway as well
        /// as an optional NAT enabling external connectivity.
        /// </summary>
        /// <param name="switchName">The new switch name.</param>
        /// <param name="subnet">Specifies the internal subnet.</param>
        /// <param name="addNat">Optionally configure a NAT to support external routing.</param>
        public void NewInternalSwitch(string switchName, NetworkCidr subnet, bool addNat = false)
        {
            if (isAdmin)
            {
                hypervClient.NewInternalSwitch(switchName: switchName, subnet: subnet, addNat: addNat);
            }
            else
            {
                var request = new GrpcNewInternalSwitchRequest(switchName: switchName, subnet: subnet, addNat: addNat);
                var reply   = desktopService.NewInternalSwitchAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Removes a named virtual switch, it it exists as well as any associated NAT (with the same name).
        /// </summary>
        /// <param name="switchName">The target switch name.</param>
        /// <param name="ignoreMissing">Optionally ignore missing items.</param>
        public void RemoveSwitch(string switchName, bool ignoreMissing = false)
        {
            if (isAdmin)
            {
                hypervClient.RemoveSwitch(switchName: switchName, ignoreMissing: ignoreMissing);
            }
            else
            {
                var request = new GrpcRemoveSwitchRequest(switchName: switchName, ignoreMissing: ignoreMissing);
                var reply   = desktopService.RemoveSwitchAsync(request).Result;

                reply.Error.EnsureSuccess();
            }
        }

        /// <summary>
        /// Returns the virtual network adapters attached to the named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <param name="waitForAddresses">Optionally wait until at least one adapter has been able to acquire at least one IPv4 address.</param>
        /// <returns>The list of network adapters.</returns>
        public List<VirtualNetworkAdapter> GetVmNetworkAdapters(string machineName, bool waitForAddresses = false)
        {
            if (isAdmin)
            {
                return hypervClient.GetVmNetworkAdapters(machineName: machineName, waitForAddresses: waitForAddresses);
            }
            else
            {
                var request = new GrpcGetVmNetworkAdaptersRequest(machineName: machineName, waitForAddresses: waitForAddresses);
                var reply   = desktopService.GetVmNetworkAdaptersAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Adapters.Select(adapter => adapter.ToLocal()).ToList();
            }
        }

        /// <summary>
        /// Lists the virtual NATs.
        /// </summary>
        /// <returns>A list of <see cref="VirtualNat"/>.</returns>
        public List<VirtualNat> ListNats()
        {
            if (isAdmin)
            {
                return hypervClient.ListNats();
            }
            else
            {
                var request = new GrpcListNatsRequest();
                var reply   = desktopService.ListNatsAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Nats.Select(nat => nat.ToLocal()).ToList();
            }
        }

        /// <summary>
        /// Looks for a virtual NAT by name.
        /// </summary>
        /// <param name="name">The desired NAT name.</param>
        /// <returns>The <see cref="VirtualNat"/> or <c>null</c> if the NAT doesn't exist.</returns>
        public VirtualNat GetNatByName(string name)
        {
            if (isAdmin)
            {
                return hypervClient.GetNatByName(name: name);
            }
            else
            {
                var request = new GrpcGetNatByNameRequest(name: name);
                var reply   = desktopService.GetNatByNameAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Nat.ToLocal();
            }
        }

        /// <summary>
        /// Looks for a virtual NAT by subnet.
        /// </summary>
        /// <param name="subnet">The desired NAT subnet.</param>
        /// <returns>The <see cref="VirtualNat"/> or <c>null</c> if the NAT doesn't exist.</returns>
        public VirtualNat GetNatBySubnet(string subnet)
        {
            if (isAdmin)
            {
                return hypervClient.GetNatBySubnet(subnet: subnet);
            }
            else
            {
                var request = new GrpcGetNatBySubnetRequest(subnet: subnet);
                var reply   = desktopService.GetNatByNameSubnetAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Nat.ToLocal();
            }
        }

        /// <summary>
        /// Returns information about a virtual IP address.
        /// </summary>
        /// <param name="address">The desired IP address.</param>
        /// <returns>The <see cref="VirtualIPAddress"/> or <c>null</c> when it doesn't exist.</returns>
        public VirtualIPAddress GetIPAddress(string address)
        {
            if (isAdmin)
            {
                return hypervClient.GetIPAddress(address: address);
            }
            else
            {
                var request = new GrpcGetIPAddressRequest(address: address);
                var reply   = desktopService.GetIPAddressAsync(request).Result;

                reply.Error.EnsureSuccess();

                return reply.Address.ToLocal();
            }
        }
    }
}
