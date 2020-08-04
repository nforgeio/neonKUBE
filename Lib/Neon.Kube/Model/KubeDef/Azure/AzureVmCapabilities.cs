//-----------------------------------------------------------------------------
// FILE:	    AzureVmCapabilities.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the capabilities of the Azure virtual machines.
    /// </summary>
    public class AzureVmCapabilities
    {
        //---------------------------------------------------------------------
        // Static members

        private static Dictionary<AzureVmSizes, AzureVmCapabilities> vmSizeToCapabilities;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static AzureVmCapabilities()
        {
            vmSizeToCapabilities = new Dictionary<AzureVmSizes, AzureVmCapabilities>();

            AzureVmCapabilities caps;

            //-----------------------------------------------------------------
            // Standard-A

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A1, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 1,
                RamMiB = 1750,
                EphemeralDriveGiB = 225,
                EphemeralDriveSSD = false,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A2, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 2,
                RamMiB = 3500,
                EphemeralDriveGiB = 490,
                EphemeralDriveSSD = false,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A3, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 4,
                RamMiB = 7000,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = false,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A4, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 8,
                RamMiB = 1400,
                EphemeralDriveGiB = 2040,
                EphemeralDriveSSD = false,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A5, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 1,
                RamMiB = 14000,
                EphemeralDriveGiB = 490,
                EphemeralDriveSSD = false,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A6, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 4,
                RamMiB = 2800,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = false,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A7, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 8,
                RamMiB = 56000,
                EphemeralDriveGiB = 2040,
                EphemeralDriveSSD = false,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-A-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A1_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 1,
                RamMiB = 2000,
                EphemeralDriveGiB = 10,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A2_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 20,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A4_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 1,
                RamMiB = 8000,
                EphemeralDriveGiB = 40,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A8_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 8,
                RamMiB = 16000,
                EphemeralDriveGiB = 80,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A2M_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 2,
                RamMiB = 16000,
                EphemeralDriveGiB = 20,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A4M_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 4,
                RamMiB = 32000,
                EphemeralDriveGiB = 40,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A8M_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 81,
                RamMiB = 64000,
                EphemeralDriveGiB = 80,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-B

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B1S, AzureStorageTypes.StandardHDD, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 1,
                RamMiB = 1000,
                EphemeralDriveGiB = 4,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B1MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 1,
                RamMiB = 2000,
                EphemeralDriveGiB = 4,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B2S, AzureStorageTypes.StandardHDD, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 8,
                EphemeralDriveSSD = true,
                MaxNics = 3,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B2MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 8000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxNics = 3,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B4MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 16000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B8MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 32000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 16
            };

            //-----------------------------------------------------------------
            // Standard-DC

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DC2S, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 2,
                RamMiB = 8000,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DC4S, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 4,
                RamMiB = 16000,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-D-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D1_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 1,
                RamMiB = 3500,
                EphemeralDriveGiB = 50,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D2_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 2,
                RamMiB = 7000,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D3_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 4,
                RamMiB = 14000,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D4_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 8,
                RamMiB = 28000,
                EphemeralDriveGiB = 400,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D5_v2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 16,
                RamMiB = 56000,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D11_v2, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 2,
                RamMiB = 14000,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D12_v2, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 4,
                RamMiB = 28000,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D13_v2, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 8,
                RamMiB = 56000,
                EphemeralDriveGiB = 400,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D14_v2, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 16,
                RamMiB = 112000,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D15_v2, AzureStorageTypes.StandardHDD)
            {
                IsDeprecated = true,
                CoreCount = 20,
                RamMiB = 140000,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 40
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-DS-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS1_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 1,
                RamMiB = 3500,
                EphemeralDriveGiB = 7,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS2_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 7000,
                EphemeralDriveGiB = 14,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS3_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 14000,
                EphemeralDriveGiB = 28,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS4_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 28000,
                EphemeralDriveGiB = 56,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS5_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 56000,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS11_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                IsDeprecated = true,
                CoreCount = 2,
                RamMiB = 14000,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS12_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                IsDeprecated = true,
                CoreCount = 4,
                RamMiB = 28000,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS13_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                IsDeprecated = true,
                CoreCount = 8,
                RamMiB = 56000,
                EphemeralDriveGiB = 400,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS14_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                IsDeprecated = true,
                CoreCount = 16,
                RamMiB = 12000,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS15_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                IsDeprecated = true,
                CoreCount = 20,
                RamMiB = 140000,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 40
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-DS-V3

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D2S_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 8000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D4S_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 16000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D8s_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 32000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D16S_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 64000,
                EphemeralDriveGiB = 128,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D32S_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 32,
                RamMiB = 128000,
                EphemeralDriveGiB = 256,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D64S_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 512000,
                EphemeralDriveGiB = 512,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-F

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F1, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 1,
                RamMiB = 2000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F4, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 4,
                RamMiB = 8000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F8, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 8,
                RamMiB = 16000,
                EphemeralDriveGiB = 128,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F16, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 16,
                RamMiB = 32000,
                EphemeralDriveGiB = 256,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-FS

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F1S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 1,
                RamMiB = 2000,
                EphemeralDriveGiB = 4,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F2S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 8,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F4S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 8000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F8S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 16000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F16S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 32000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-E-V3

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_E2_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 16,
                EphemeralDriveGiB = 50,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_E4_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 32,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_E8_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 64,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_E16_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 128,
                EphemeralDriveGiB = 400,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_E20_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 20,
                RamMiB = 160,
                EphemeralDriveGiB = 500,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_E32_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 32,
                RamMiB = 256,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_E64_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 432,
                EphemeralDriveGiB = 1600,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_E64I_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 432,
                EphemeralDriveGiB = 1600,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-ES-V3

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ES2_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 16,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ES4_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 32,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ES8_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 64,
                EphemeralDriveGiB = 128,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ES16_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 128,
                EphemeralDriveGiB = 256,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ES20_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 20,
                RamMiB = 160,
                EphemeralDriveGiB = 320,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ES32_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 32,
                RamMiB = 256,
                EphemeralDriveGiB = 512,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ES64_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 432,
                EphemeralDriveGiB = 864,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ES64I_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 432,
                EphemeralDriveGiB = 864,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-FS-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F2S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F4S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 8000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F8S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 16000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F16S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 32,
                EphemeralDriveGiB = 128,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F32S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 32,
                RamMiB = 64,
                EphemeralDriveGiB = 256,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F64S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 128,
                EphemeralDriveGiB = 512,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F72S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 72,
                RamMiB = 144,
                EphemeralDriveGiB = 576,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-G

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G1, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 2,
                RamMiB = 28000,
                EphemeralDriveGiB = 384,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G2, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 4,
                RamMiB = 56000,
                EphemeralDriveGiB = 768,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G3, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 8,
                RamMiB = 112000,
                EphemeralDriveGiB = 1536,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G4, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 16,
                RamMiB = 224000,
                EphemeralDriveGiB = 3072,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G5, AzureStorageTypes.StandardHDD)
            {
                CoreCount = 32,
                RamMiB = 448000,
                EphemeralDriveGiB = 6144,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-GS

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS1, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 2,
                RamMiB = 28000,
                EphemeralDriveGiB = 384,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 56000,
                EphemeralDriveGiB = 768,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 112000,
                EphemeralDriveGiB = 1536,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS4, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 224000,
                EphemeralDriveGiB = 3072,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS5, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 32,
                RamMiB = 448000,
                EphemeralDriveGiB = 6144,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-LS-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_L4S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 4,
                RamMiB = 32,
                EphemeralDriveGiB = 678,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_L8S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 64,
                EphemeralDriveGiB = 1388,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_L16S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 128,
                EphemeralDriveGiB = 2807,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_L32S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 32,
                RamMiB = 256,
                EphemeralDriveGiB = 5630,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-H

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_H8, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 56,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_H8M, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 112,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = true,
                MaxNics = 2,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_H16, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 112,
                EphemeralDriveGiB = 2000,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_H16M, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 224,
                EphemeralDriveGiB = 2000,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_H16MR, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 224,
                EphemeralDriveGiB = 2000,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_H16R, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 112,
                EphemeralDriveGiB = 2000,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-M

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M8MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 8,
                RamMiB = 218750,
                EphemeralDriveGiB = 256,
                EphemeralDriveSSD = true,
                MaxNics = 4,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M16MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 16,
                RamMiB = 437500,
                EphemeralDriveGiB = 512,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M32TS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 32,
                RamMiB = 192000,
                EphemeralDriveGiB = 1024,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M32LS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 256000,
                EphemeralDriveGiB = 1024,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M32MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 875000,
                EphemeralDriveGiB = 1024,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M64, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 1024000,
                EphemeralDriveGiB = 7168,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M64MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 1792000,
                EphemeralDriveGiB = 2048,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M64M, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 1792000,
                EphemeralDriveGiB = 7168,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M64S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 1024000,
                EphemeralDriveGiB = 2048,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M64LS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 64,
                RamMiB = 512000,
                EphemeralDriveGiB = 2048,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M128, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 128,
                RamMiB = 2048000,
                EphemeralDriveGiB = 14336,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M128M, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 128,
                RamMiB = 3892000,
                EphemeralDriveGiB = 14336,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M128MS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 128,
                RamMiB = 3892000,
                EphemeralDriveGiB = 4096,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_M128S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 128,
                RamMiB = 2048,
                EphemeralDriveGiB = 4096,
                EphemeralDriveSSD = true,
                MaxNics = 8,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-NC

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC6, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 6,
                RamMiB = 56,
                EphemeralDriveGiB = 340,
                EphemeralDriveSSD = true,
                MaxDataDrives = 24,
                MaxNics = 1,
                GpuCount = 1,
                GpuRamGiB = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC12, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 12,
                RamMiB = 112,
                EphemeralDriveGiB = 680,
                EphemeralDriveSSD = true,
                MaxDataDrives = 48,
                MaxNics = 2,
                GpuCount = 2,
                GpuRamGiB = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC24, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 224,
                EphemeralDriveGiB = 1440,
                EphemeralDriveSSD = true,
                MaxDataDrives = 64,
                MaxNics = 4,
                GpuCount = 4,
                GpuRamGiB = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC24R, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 224,
                EphemeralDriveGiB = 1440,
                EphemeralDriveSSD = true,
                MaxDataDrives = 64,
                MaxNics = 4,
                GpuCount = 4,
                GpuRamGiB = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-NC-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC6S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 6,
                RamMiB = 112,
                EphemeralDriveGiB = 736,
                EphemeralDriveSSD = true,
                MaxDataDrives = 12,
                MaxNics = 4,
                GpuCount = 1,
                GpuRamGiB = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC12S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 12,
                RamMiB = 224,
                EphemeralDriveGiB = 1474,
                EphemeralDriveSSD = true,
                MaxDataDrives = 24,
                MaxNics = 8,
                GpuCount = 2,
                GpuRamGiB = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC24S_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 448,
                EphemeralDriveGiB = 2948,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32,
                MaxNics = 8,
                GpuCount = 4,
                GpuRamGiB = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC24RS_v2, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 448,
                EphemeralDriveGiB = 2948,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32,
                MaxNics = 8,
                GpuCount = 4,
                GpuRamGiB = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-NC-V3

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC6S_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 6,
                RamMiB = 112,
                EphemeralDriveGiB = 736,
                EphemeralDriveSSD = true,
                MaxDataDrives = 12,
                MaxNics = 4,
                GpuCount = 1,
                GpuRamGiB = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC12S_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 12,
                RamMiB = 224,
                EphemeralDriveGiB = 1474,
                EphemeralDriveSSD = true,
                MaxDataDrives = 24,
                MaxNics = 8,
                GpuCount = 2,
                GpuRamGiB = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC24S_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 448,
                EphemeralDriveGiB = 2948,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32,
                MaxNics = 8,
                GpuCount = 4,
                GpuRamGiB = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NC24RS_v3, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 448,
                EphemeralDriveGiB = 2948,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32,
                MaxNics = 4,
                GpuCount = 8,
                GpuRamGiB = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-ND

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ND6S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 6,
                RamMiB = 112,
                EphemeralDriveGiB = 736,
                EphemeralDriveSSD = true,
                MaxDataDrives = 12,
                MaxNics = 4,
                GpuCount = 1,
                GpuRamGiB = 24
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ND12S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 12,
                RamMiB = 224,
                EphemeralDriveGiB = 1474,
                EphemeralDriveSSD = true,
                MaxDataDrives = 24,
                MaxNics = 8,
                GpuCount = 2,
                GpuRamGiB = 48
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ND24S, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 448,
                EphemeralDriveGiB = 2948,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32,
                MaxNics = 8,
                GpuCount = 4,
                GpuRamGiB = 96
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_ND24RS, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 448,
                EphemeralDriveGiB = 2948,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32,
                MaxNics = 8,
                GpuCount = 4,
                GpuRamGiB = 96
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-NV

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NV6, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 6,
                RamMiB = 56,
                EphemeralDriveGiB = 340,
                EphemeralDriveSSD = true,
                MaxDataDrives = 24,
                MaxNics = 1,
                GpuCount = 1,
                GpuRamGiB = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NV12, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 12,
                RamMiB = 112,
                EphemeralDriveGiB = 680,
                EphemeralDriveSSD = true,
                MaxDataDrives = 48,
                MaxNics = 2,
                GpuCount = 2,
                GpuRamGiB = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_NV24, AzureStorageTypes.StandardHDD, AzureStorageTypes.PremiumSSD)
            {
                CoreCount = 24,
                RamMiB = 224,
                EphemeralDriveGiB = 1440,
                EphemeralDriveSSD = true,
                MaxDataDrives = 64,
                MaxNics = 4,
                GpuCount = 4,
                GpuRamGiB = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Verify that we have a valid definition for every know MV size.

            var sbError = new StringBuilder();

            foreach (AzureVmSizes vmSize in Enum.GetValues(typeof(AzureVmSizes)))
            {
                if (!vmSizeToCapabilities.TryGetValue(vmSize, out caps))
                {
                    sbError.AppendLine($"[{vmSize}]: Entry is missing.");
                    continue;
                }

                if (caps.SupportedDataStorageTypes?.Count == 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.SupportedDataStorageTypes)}]: is empty.");
                }

                if (caps.CoreCount <= 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.CoreCount)}]: is not positive.");
                }

                if (caps.RamMiB <= 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.RamMiB)}]: is not positive.");
                }

                if (caps.EphemeralDriveGiB <= 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.EphemeralDriveGiB)}]: is not positive.");
                }

                if (caps.MaxNics <= 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.MaxNics)}]: is not positive.");
                }

                if (caps.MaxDataDrives <= 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.MaxDataDrives)}]: is not positive.");
                }
            }

            if (sbError.Length > 0)
            {
                var errors = sbError.ToString();

                sbError.Clear();
                sbError.AppendLine($"Coding Error: [{nameof(AzureVmCapabilities)}] definitions has one or more problems.");
                sbError.Append(errors);

                throw new Exception(sbError.ToString());
            }
        }

        /// <summary>
        /// Returns the <see cref="AzureVmCapabilities"/> for a specified VM size.
        /// </summary>
        /// <param name="vmSize">The VM size.</param>
        /// <returns>
        /// The requested <see cref="AzureVmCapabilities"/> or <c>null</c> 
        /// if information about the VM cannot be located.
        /// </returns>
        public static AzureVmCapabilities Get(AzureVmSizes vmSize)
        {
            AzureVmCapabilities result;

            if (vmSizeToCapabilities.TryGetValue(vmSize, out result))
            {
                return result;
            }
            else
            {
                return null; // Shouldn't ever happen.
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="vmSize">The VM size.</param>
        /// <param name="storageTypes">The supported storage types.</param>
        private AzureVmCapabilities(AzureVmSizes vmSize, params AzureStorageTypes[] storageTypes)
        {
            this.VmSize = vmSize;
            this.SupportedDataStorageTypes = new HashSet<AzureStorageTypes>();

            foreach (var type in storageTypes)
            {
                this.SupportedDataStorageTypes.Add(type);
            }

            // Note that all Azure VMs can support standard SSDs so we'll
            // add that here if it's not already specified.

            if (!this.SupportedDataStorageTypes.Contains(AzureStorageTypes.StandardSSD))
            {
                this.SupportedDataStorageTypes.Add(AzureStorageTypes.StandardSSD);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if Azure has deprecated the VM size.
        /// </summary>
        public bool IsDeprecated { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if this VM size is not supported by clusters.
        /// </summary>
        public bool NotSupported { get; private set; } = false;

        /// <summary>
        /// Returns the associated <see cref="AzureVmSizes"/> value.
        /// </summary>
        public AzureVmSizes VmSize { get; private set; }

        /// <summary>
        /// Returns the number of virtual CPU cores provided for the VM type.
        /// </summary>
        public int CoreCount { get; private set; }

        /// <summary>
        /// Returns RAM provided for the VM type in MiB.
        /// </summary>
        public int RamMiB { get; private set; }

        /// <summary>
        /// Returns the size of the VM ephemeral drive in GiB.
        /// </summary>
        public int EphemeralDriveGiB { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the VM ephemeral drive is an SSD.
        /// </summary>
        public bool EphemeralDriveSSD { get; private set; }

        /// <summary>
        /// Returns the number of data drives that can be attached to the VM.
        /// </summary>
        public int MaxDataDrives { get; private set; }

        /// <summary>
        /// Returns the maximum number of network interfaces that can be attached.
        /// </summary>
        public int MaxNics { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the VM supports load balancing.
        /// </summary>
        public bool LoadBalancing { get; private set; } = true;

        /// <summary>
        /// Returns the number of GPUs.
        /// </summary>
        public int GpuCount { get; private set; } = 0;

        /// <summary>
        /// Returns the amount of GPU RAM in GiB.
        /// </summary>
        public int GpuRamGiB { get; private set; } = 0;

        /// <summary>
        /// Hash set of the supported data drive storage account types.
        /// </summary>
        private HashSet<AzureStorageTypes> SupportedDataStorageTypes { get; set;}

        /// <summary>
        /// Determines whether the VM supports data drives with a specific storage account type.
        /// </summary>
        /// <param name="storageType">The storage type being tested.</param>
        /// <returns><c>true</c> if the storage type is supported.</returns>
        public bool SupportsDataStorageType(AzureStorageTypes storageType)
        {
            return SupportedDataStorageTypes.Contains(storageType);
        }
    }
}
