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
                RamSizeMB = 1750,
                EphemeralDriveGB = 225,
                EphemeralDriveSSD = false,
                DataDriveCount = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 3500,
                EphemeralDriveGB = 490,
                EphemeralDriveSSD = false,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A3, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 7000,
                EphemeralDriveGB = 1000,
                EphemeralDriveSSD = false,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A4, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 1400,
                EphemeralDriveGB = 2040,
                EphemeralDriveSSD = false,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A5, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamSizeMB = 14000,
                EphemeralDriveGB = 490,
                EphemeralDriveSSD = false,
                DataDriveCount = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A6, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 2800,
                EphemeralDriveGB = 1000,
                EphemeralDriveSSD = false,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_A7, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 56000,
                EphemeralDriveGB = 2040,
                EphemeralDriveSSD = false,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-D-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D1_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamSizeMB = 3500,
                EphemeralDriveGB = 50,
                EphemeralDriveSSD = true,
                DataDriveCount = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D2_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 7000,
                EphemeralDriveGB = 100,
                EphemeralDriveSSD = true,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D3_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 14000,
                EphemeralDriveGB = 200,
                EphemeralDriveSSD = true,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D4_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 28000,
                EphemeralDriveGB = 400,
                EphemeralDriveSSD = true,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D5_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 16,
                RamSizeMB = 56000,
                EphemeralDriveGB = 800,
                EphemeralDriveSSD = true,
                DataDriveCount = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D11_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 14000,
                EphemeralDriveGB = 100,
                EphemeralDriveSSD = true,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D12_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 28000,
                EphemeralDriveGB = 200,
                EphemeralDriveSSD = true,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D13_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 56000,
                EphemeralDriveGB = 400,
                EphemeralDriveSSD = true,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D14_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 16,
                RamSizeMB = 112000,
                EphemeralDriveGB = 800,
                EphemeralDriveSSD = true,
                DataDriveCount = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_D15_v2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 20,
                RamSizeMB = 140000,
                EphemeralDriveGB = 1000,
                EphemeralDriveSSD = true,
                DataDriveCount = 40
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-DS-V2

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS1_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 1,
                RamSizeMB = 3500,
                EphemeralDriveGB = 7,
                EphemeralDriveSSD = true,
                DataDriveCount = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS2_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 7000,
                EphemeralDriveGB = 14,
                EphemeralDriveSSD = true,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS3_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 14000,
                EphemeralDriveGB = 28,
                EphemeralDriveSSD = true,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS4_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 28000,
                EphemeralDriveGB = 56,
                EphemeralDriveSSD = true,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS5_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamSizeMB = 56000,
                EphemeralDriveGB = 800,
                EphemeralDriveSSD = true,
                DataDriveCount = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS11_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 14000,
                EphemeralDriveGB = 100,
                EphemeralDriveSSD = true,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS12_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 28000,
                EphemeralDriveGB = 200,
                EphemeralDriveSSD = true,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS13_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 56000,
                EphemeralDriveGB = 400,
                EphemeralDriveSSD = true,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS14_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamSizeMB = 12000,
                EphemeralDriveGB = 800,
                EphemeralDriveSSD = true,
                DataDriveCount = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_DS15_v2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 20,
                RamSizeMB = 140000,
                EphemeralDriveGB = 1000,
                EphemeralDriveSSD = true,
                DataDriveCount = 40
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-G

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G1, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 28000,
                EphemeralDriveGB = 384,
                EphemeralDriveSSD = true,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 56000,
                EphemeralDriveGB = 768,
                EphemeralDriveSSD = true,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G3, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 112000,
                EphemeralDriveGB = 1536,
                EphemeralDriveSSD = true,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G4, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 16,
                RamSizeMB = 224000,
                EphemeralDriveGB = 3072,
                EphemeralDriveSSD = true,
                DataDriveCount = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_G5, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 32,
                RamSizeMB = 448000,
                EphemeralDriveGB = 6144,
                EphemeralDriveSSD = true,
                DataDriveCount = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-GS

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS1, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 28000,
                EphemeralDriveGB = 384,
                EphemeralDriveSSD = true,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS2, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 56000,
                EphemeralDriveGB = 768,
                EphemeralDriveSSD = true,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS3, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 112000,
                EphemeralDriveGB = 1536,
                EphemeralDriveSSD = true,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS4, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamSizeMB = 224000,
                EphemeralDriveGB = 3072,
                EphemeralDriveSSD = true,
                DataDriveCount = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_GS5, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 32,
                RamSizeMB = 448000,
                EphemeralDriveGB = 6144,
                EphemeralDriveSSD = true,
                DataDriveCount = 64
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-F

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F1, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 1,
                RamSizeMB = 2000,
                EphemeralDriveGB = 1 * 64,
                EphemeralDriveSSD = true,
                DataDriveCount = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F2, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 4000,
                EphemeralDriveGB = 2 * 64,
                EphemeralDriveSSD = true,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F4, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 8000,
                EphemeralDriveGB = 4 * 64,
                EphemeralDriveSSD = true,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F8, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 16000,
                EphemeralDriveGB = 16 * 64,
                EphemeralDriveSSD = true,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F16, AzureStorageTypes.StandardHDD_LRS)
            {
                CoreCount = 16,
                RamSizeMB = 32000,
                EphemeralDriveGB = 6144,
                EphemeralDriveSSD = true,
                DataDriveCount = 32
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            //-----------------------------------------------------------------
            // Standard-FS

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F1s, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 1,
                RamSizeMB = 2000,
                EphemeralDriveGB = 1 * 64,
                EphemeralDriveSSD = true,
                DataDriveCount = 2
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F2s, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 2,
                RamSizeMB = 4000,
                EphemeralDriveGB = 2 * 64,
                EphemeralDriveSSD = true,
                DataDriveCount = 4
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F4s, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 4,
                RamSizeMB = 8000,
                EphemeralDriveGB = 4 * 64,
                EphemeralDriveSSD = true,
                DataDriveCount = 8
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F8s, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 8,
                RamSizeMB = 16000,
                EphemeralDriveGB = 16 * 64,
                EphemeralDriveSSD = true,
                DataDriveCount = 16
            };

            vmSizeToCapabilities.Add(caps.VmSize, caps);

            caps = new AzureVmCapabilities(AzureVmSizes.Standard_F16s, AzureStorageTypes.StandardHDD_LRS, AzureStorageTypes.PremiumSSD_LRS)
            {
                CoreCount = 16,
                RamSizeMB = 32000,
                EphemeralDriveGB = 6144,
                EphemeralDriveSSD = true,
                DataDriveCount = 32
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

                if (caps.RamSizeMB <= 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.RamSizeMB)}]: is not positive.");
                }

                if (caps.EphemeralDriveGB <= 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.EphemeralDriveGB)}]: is not positive.");
                }

                if (caps.DataDriveCount <= 0)
                {
                    sbError.AppendLine($"[{vmSize}.{nameof(AzureVmCapabilities.DataDriveCount)}]: is not positive.");
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
        /// Returns the number of megabytes of RAM provided for the VM type.
        /// </summary>
        public int RamSizeMB { get; private set; }

        /// <summary>
        /// Returns the size of the VM ephemeral drive in gigabytes.
        /// </summary>
        public int EphemeralDriveGB { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the VM ephemeral drive is an SSD.
        /// </summary>
        public bool EphemeralDriveSSD { get; private set; }

        /// <summary>
        /// Returns the number of data drives that can be attached to the VM.
        /// </summary>
        public int DataDriveCount { get; private set; }

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
