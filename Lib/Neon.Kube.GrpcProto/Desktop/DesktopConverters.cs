//-----------------------------------------------------------------------------
// FILE:        DesktopConverters.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.HyperV;
using Neon.Kube.GrpcProto.Desktop;
using Neon.Net;

namespace Neon.Kube.GrpcProto.Desktop
{
    /// <summary>
    /// Conversions between gRPC proto and local types.
    /// </summary>
    public static class DesktopConverters
    {
        //---------------------------------------------------------------------
        // VirtualDrive

        /// <summary>
        /// Converts a <see cref="GrpcVirtualDrive"/> to a <see cref="VirtualDrive"/>.
        /// </summary>
        /// <param name="grpcVirtualDrive">The input.</param>
        /// <returns>The output.</returns>
        public static VirtualDrive? ToLocal(this GrpcVirtualDrive grpcVirtualDrive)
        {
            if (grpcVirtualDrive == null)
            {
                return null;
            }

            return new VirtualDrive()
            {
                Path      = grpcVirtualDrive.Path,
                Size      = grpcVirtualDrive.Size,
                IsDynamic = grpcVirtualDrive.IsDynamic
            };
        }

        /// <summary>
        /// Converts a <see cref="VirtualDrive"/> to a <see cref="GrpcVirtualDrive"/>.
        /// </summary>
        /// <param name="virtualDrive">The input.</param>
        /// <returns>The output.</returns>
        public static GrpcVirtualDrive? ToProto(this VirtualDrive virtualDrive)
        {
            if (virtualDrive == null)
            {
                return null;
            }

            return new GrpcVirtualDrive(
                path:      virtualDrive.Path,
                size:      (long)virtualDrive.Size,
                isDynamic: virtualDrive.IsDynamic);
        }

        //---------------------------------------------------------------------
        // VirtualNat

        /// <summary>
        /// Converts a <see cref="GrpcVirtualNat"/> tp a <see cref="VirtualNat"/>.
        /// </summary>
        /// <param name="grpcVirtualNat">The input.</param>
        /// <returns>The output.</returns>
        public static VirtualNat? ToLocal(this GrpcVirtualNat grpcVirtualNat)
        {
            if (grpcVirtualNat == null)
            {
                return null;
            }

            return new VirtualNat()
            {
                 Name   = grpcVirtualNat.Name,
                 Subnet = grpcVirtualNat.Subnet
            };
        }

        /// <summary>
        /// Comverts a <see cref="VirtualNat"/> into a <see cref="GrpcVirtualNat"/>.
        /// </summary>
        /// <param name="virtualNat">The input.</param>
        /// <returns>The output.</returns>
        public static GrpcVirtualNat? ToProto(this VirtualNat virtualNat)
        {
            if (virtualNat == null)
            {
                return null;
            }

            return new GrpcVirtualNat(name: virtualNat.Name, subnet: virtualNat.Subnet);
        }

        //---------------------------------------------------------------------
        // VirtualSwirch

        /// <summary>
        /// Converts a <see cref="GrpcVirtualSwitch"/> tp a <see cref="VirtualSwitch"/>.
        /// </summary>
        /// <param name="grpcVirtualSwitch">The input.</param>
        /// <returns>The output.</returns>
        public static VirtualSwitch? ToLocal(this GrpcVirtualSwitch grpcVirtualSwitch)
        {
            if (grpcVirtualSwitch == null)
            {
                return null;
            }

            return new VirtualSwitch()
            {
                Name = grpcVirtualSwitch.Name,
                Type = NeonHelper.ParseEnum<VirtualSwitchType>(grpcVirtualSwitch.Type)
            };
        }

        /// <summary>
        /// Comverts a <see cref="VirtualSwitch"/> into a <see cref="GrpcVirtualSwitch"/>.
        /// </summary>
        /// <param name="virtualSwitch">The input.</param>
        /// <returns>The output.</returns>
        public static GrpcVirtualSwitch? ToProto(this VirtualSwitch virtualSwitch)
        {
            if (virtualSwitch == null)
            {
                return  null;
            }

            return new GrpcVirtualSwitch(
                name: virtualSwitch.Name,
                type: NeonHelper.EnumToString(virtualSwitch.Type));
        }

        //---------------------------------------------------------------------
        // VirtualMachine

        /// <summary>
        /// Converts a <see cref="GrpcVirtualMachine"/> tp a <see cref="VirtualMachine"/>.
        /// </summary>
        /// <param name="grpcVirtualMachine">The input.</param>
        /// <returns>The output.</returns>
        public static VirtualMachine? ToLocal(this GrpcVirtualMachine grpcVirtualMachine)
        {
            if (grpcVirtualMachine == null)
            {
                return null;
            }

            return new VirtualMachine()
            {
                Name           = grpcVirtualMachine.Name,
                State          = NeonHelper.ParseEnum<VirtualMachineState>(grpcVirtualMachine.State),
                NetAdapterName = grpcVirtualMachine.InterfaceName,
                SwitchName     = grpcVirtualMachine.SwitchName
            };
        }

        /// <summary>
        /// Comverts a <see cref="VirtualMachine"/> into a <see cref="GrpcVirtualMachine"/>.
        /// </summary>
        /// <param name="virtualMachine">The input.</param>
        /// <returns>The output.</returns>
        public static GrpcVirtualMachine? ToProto(this VirtualMachine virtualMachine)
        {
            if (virtualMachine == null)
            {
                return null;
            }

            return new GrpcVirtualMachine(
                name:           virtualMachine.Name,
                state:          NeonHelper.EnumToString(virtualMachine.State),
                netAdapterName: virtualMachine.NetAdapterName,
                switchName:     virtualMachine.SwitchName);
        }

        //---------------------------------------------------------------------
        // VirtualMachineNetworkAdapter

        /// <summary>
        /// Converts a <see cref="GrpcVirtualMachineNetworkAdapter"/> into a <see cref="VirtualMachineNetworkAdapter"/>.
        /// </summary>
        /// <param name="grpcVirtualMachineNetworkAdapter">The input.</param>
        /// <returns>The output.</returns>
        public static VirtualMachineNetworkAdapter? ToLocal(this GrpcVirtualMachineNetworkAdapter grpcVirtualMachineNetworkAdapter)
        {
            if (grpcVirtualMachineNetworkAdapter == null)
            {
                return null;
            }

            return new VirtualMachineNetworkAdapter()
            {
                Name           = grpcVirtualMachineNetworkAdapter.Name,
                SwitchName     = grpcVirtualMachineNetworkAdapter.SwitchName,
                IsManagementOs = grpcVirtualMachineNetworkAdapter.IsManagementOs,
                MacAddress     = grpcVirtualMachineNetworkAdapter.MacAddress,
                Addresses      = grpcVirtualMachineNetworkAdapter.Addresses?.Select(address => IPAddress.Parse(address)).ToList(),
                Status         = grpcVirtualMachineNetworkAdapter.Status,
                VMName         = grpcVirtualMachineNetworkAdapter.VMName
            };
        }

        /// <summary>
        /// Convertsa <see cref="VirtualMachineNetworkAdapter"/> int a <see cref="GrpcVirtualMachineNetworkAdapter"/>.
        /// </summary>
        /// <param name="virtualNat">The input.</param>
        /// <returns>The output.</returns>
        public static GrpcVirtualMachineNetworkAdapter? ToProto(this VirtualMachineNetworkAdapter virtualNat)
        {
            if (virtualNat == null)
            {
                return null;
            }

            return new GrpcVirtualMachineNetworkAdapter(
                name:           virtualNat.Name,
                switchName:     virtualNat.SwitchName,
                isManagementOs: virtualNat.IsManagementOs,
                macAddress:     virtualNat.MacAddress,
                addresses:      virtualNat.Addresses.Select(address => address.ToString()).ToList(),
                status:         virtualNat.Status,
                vmName:         virtualNat.VMName);
        }

        //---------------------------------------------------------------------
        // VirtualIPAddress

        /// <summary>
        /// Converts a <see cref="GrpcVirtualIPAddress"/> into a <see cref="VirtualIPAddress"/>.
        /// </summary>
        /// <param name="grpcVirtualIPAddress">The input.</param>
        /// <returns>The output.</returns>
        public static VirtualIPAddress? ToLocal(this GrpcVirtualIPAddress grpcVirtualIPAddress)
        {
            if (grpcVirtualIPAddress == null)
            {
                return null;
            }

            return new VirtualIPAddress()
            {
                Address       = grpcVirtualIPAddress.Address,
                Subnet        = NetworkCidr.Parse(grpcVirtualIPAddress.Subnet),
                InterfaceName = grpcVirtualIPAddress.InterfaceName
            };
        }

        /// <summary>
        /// Converts a <see cref="VirtualIPAddress"/> into a <see cref="GrpcVirtualIPAddress"/>.
        /// </summary>
        /// <param name="virtualIPAddress">The input</param>
        /// <returns>The output.</returns>
        public static GrpcVirtualIPAddress? ToProto(this VirtualIPAddress virtualIPAddress)
        {
            if (virtualIPAddress == null)
            {
                return null;
            }

            return new GrpcVirtualIPAddress(
                address:       virtualIPAddress.Address,
                subnet:        virtualIPAddress.Subnet,
                interfaceName: virtualIPAddress.InterfaceName);
        }
    }
}
