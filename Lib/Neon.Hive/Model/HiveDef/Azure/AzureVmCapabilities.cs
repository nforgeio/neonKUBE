//-----------------------------------------------------------------------------
// FILE:	    AzureVmCapabilities.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Hive
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

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A1, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamMiB = 1750,
                EphemeralDriveGiB = 225,
                EphemeralDriveSSD = false,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamMiB = 3500,
                EphemeralDriveGiB = 490,
                EphemeralDriveSSD = false,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A3, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamMiB = 7000,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = false,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A4, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamMiB = 1400,
                EphemeralDriveGiB = 2040,
                EphemeralDriveSSD = false,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A5, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamMiB = 14000,
                EphemeralDriveGiB = 490,
                EphemeralDriveSSD = false,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A6, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamMiB = 2800,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = false,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A7, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamMiB = 56000,
                EphemeralDriveGiB = 2040,
                EphemeralDriveSSD = false,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-A-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A1_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamMiB = 2000,
                EphemeralDriveGiB = 10,
                EphemeralDriveSSD = true,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A2_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 20,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A4_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamMiB = 8000,
                EphemeralDriveGiB = 40,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A8_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamMiB = 16000,
                EphemeralDriveGiB = 80,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A2M_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamMiB = 16000,
                EphemeralDriveGiB = 20,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A4M_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamMiB = 32000,
                EphemeralDriveGiB = 40,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A8M_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 81,
                RamMiB = 64000,
                EphemeralDriveGiB = 80,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-B

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B1S, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 1,
                RamMiB = 1000,
                EphemeralDriveGiB = 4,
                EphemeralDriveSSD = true,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B1MS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 1,
                RamMiB = 2000,
                EphemeralDriveGiB = 4,
                EphemeralDriveSSD = true,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B2S, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 8,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B2MS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamMiB = 8000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B4MS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamMiB = 16000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_B8MS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamMiB = 32000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            //-----------------------------------------------------------------
            // Standard-DC

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DC2S, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamMiB = 8000,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DC4S, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamMiB = 16000,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-D-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D1_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamMiB = 3500,
                EphemeralDriveGiB = 50,
                EphemeralDriveSSD = true,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D2_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamMiB = 7000,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D3_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamMiB = 14000,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D4_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamMiB = 28000,
                EphemeralDriveGiB = 400,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D5_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 16,
                RamMiB = 56000,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D11_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamMiB = 14000,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D12_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamMiB = 28000,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D13_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamMiB = 56000,
                EphemeralDriveGiB = 400,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D14_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 16,
                RamMiB = 112000,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D15_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 20,
                RamMiB = 140000,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = true,
                MaxDataDrives = 40
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-DS-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS1_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 1,
                RamMiB = 3500,
                EphemeralDriveGiB = 7,
                EphemeralDriveSSD = true,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS2_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamMiB = 7000,
                EphemeralDriveGiB = 14,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS3_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamMiB = 14000,
                EphemeralDriveGiB = 28,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS4_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamMiB = 28000,
                EphemeralDriveGiB = 56,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS5_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamMiB = 56000,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS11_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamMiB = 14000,
                EphemeralDriveGiB = 100,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS12_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamMiB = 28000,
                EphemeralDriveGiB = 200,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS13_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamMiB = 56000,
                EphemeralDriveGiB = 400,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS14_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamMiB = 12000,
                EphemeralDriveGiB = 800,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS15_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 20,
                RamMiB = 140000,
                EphemeralDriveGiB = 1000,
                EphemeralDriveSSD = true,
                MaxDataDrives = 40
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-DS-V3

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D2S_v3, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamMiB = 8000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D4S_v3, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamMiB = 16000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D8s_v3, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamMiB = 32000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D16S_v3, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamMiB = 64000,
                EphemeralDriveGiB = 128,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D32S_v3, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 32,
                RamMiB = 128000,
                EphemeralDriveGiB = 256,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D64S_v3, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 64,
                RamMiB = 512000,
                EphemeralDriveGiB = 512,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);


            //-----------------------------------------------------------------
            // Standard-F

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F1, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamMiB = 2000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F4, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamMiB = 8000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F8, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamMiB = 16000,
                EphemeralDriveGiB = 128,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F16, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 16,
                RamMiB = 32000,
                EphemeralDriveGiB = 256,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-FS

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F1S, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 1,
                RamMiB = 2000,
                EphemeralDriveGiB = 4,
                EphemeralDriveSSD = true,
                MaxDataDrives = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F2S, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 8,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F4S, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamMiB = 8000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F8S, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamMiB = 16000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F16S, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamMiB = 32000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-FS-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F2S_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamMiB = 4000,
                EphemeralDriveGiB = 16,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F4S_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamMiB = 8000,
                EphemeralDriveGiB = 32,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F8S_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamMiB = 16000,
                EphemeralDriveGiB = 64,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F16S_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamMiB = 32,
                EphemeralDriveGiB = 128,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F32S_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 32,
                RamMiB = 64,
                EphemeralDriveGiB = 256,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F64S_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 64,
                RamMiB = 128,
                EphemeralDriveGiB = 512,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F72S_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 72,
                RamMiB = 144,
                EphemeralDriveGiB = 576,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-G

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G1, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamMiB = 28000,
                EphemeralDriveGiB = 384,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamMiB = 56000,
                EphemeralDriveGiB = 768,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G3, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamMiB = 112000,
                EphemeralDriveGiB = 1536,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G4, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 16,
                RamMiB = 224000,
                EphemeralDriveGiB = 3072,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G5, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 32,
                RamMiB = 448000,
                EphemeralDriveGiB = 6144,
                EphemeralDriveSSD = true,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-GS

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS1, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamMiB = 28000,
                EphemeralDriveGiB = 384,
                EphemeralDriveSSD = true,
                MaxDataDrives = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamMiB = 56000,
                EphemeralDriveGiB = 768,
                EphemeralDriveSSD = true,
                MaxDataDrives = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS3, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamMiB = 112000,
                EphemeralDriveGiB = 1536,
                EphemeralDriveSSD = true,
                MaxDataDrives = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS4, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamMiB = 224000,
                EphemeralDriveGiB = 3072,
                EphemeralDriveSSD = true,
                MaxDataDrives = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS5, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 32,
                RamMiB = 448000,
                EphemeralDriveGiB = 6144,
                EphemeralDriveSSD = true,
                MaxDataDrives = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

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
        /// <returns>The requested <see cref="AzureVmCapabilities"/>.</returns>
        public static AzureVmCapabilities Get(AzureVmSizes vmSize)
        {
            AzureVmCapabilities result;

            if (vmSizeToCapabilities.TryGetValue(vmSize, out result))
            {
                return result;
            }
            else
            {
                throw new NotImplementedException(); // Shouldn't ever happen.
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
        }

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
        /// Returns <c>true</c> if the VM supports load balancing.
        /// </summary>
        public bool LoadBalancing { get; private set; } = true;

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
