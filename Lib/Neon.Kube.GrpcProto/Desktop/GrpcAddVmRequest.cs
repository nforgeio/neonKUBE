//-----------------------------------------------------------------------------
// FILE:        GrpcAddVmRequest.cs
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
    /// Creates a virtual machine.  This request returns a <see cref="GrpcBaseReply"/>.
    /// </summary>
    [DataContract]
    public class GrpcAddVmRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcAddVmRequest()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="machineName">Sperifies the machine name.</param>
        /// <param name="memorySize">
        /// Optionally specifies the memory size.  This can be a long byte count or a
        /// a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2GiB</b>, 
        /// or <b>1TiB</b>.  This defaults to <b>2GiB</b>.
        /// </param>
        /// <param name="processorCount">
        /// Optionally specifies the number of virutal processors to assign to the machine.  This defaults to <b>4</b>.
        /// </param>
        /// <param name="driveSize">
        /// Optionally specifies the primary disk size.  This can be a long byte count or
        /// a number with units like <b>512MB</b>, <b>0.5GiB</b>, <b>2GiB</b>, or <b>1TiB</b>.  
        /// Pass <c>null</c> to leave the disk alone.  This defaults to <c>null</c>.
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
        /// <param name="switchName">Optionally specifies the name of the associated virtual switch.</param>
        /// <param name="extraDrives">
        /// Optionally specifies any additional virtual drives to be created and 
        /// then attached to the new virtual machine.
        /// </param>
        /// <param name="notes">Optionally specifies any notes to persist with the VM.</param>
        /// <remarks>
        /// <note>
        /// The <see cref="GrpcVirtualDrive.Path"/> property of <paramref name="extraDrives"/> may be
        /// passed as <c>null</c> or empty.  In this case, the drive name will default to
        /// being located in the standard Hyper-V virtual drivers folder and will be named
        /// <b>MACHINE-NAME-#.vhdx</b>, where <b>#</b> is the one-based index of the drive
        /// in the enumeration.
        /// </note>
        /// </remarks>
        public GrpcAddVmRequest(
            string                          machineName,
            string                          memorySize        = "2GiB",
            int                             processorCount    = 4,
            string?                         driveSize         = null,
            string?                         drivePath         = null,
            bool                            checkpointDrives  = false,
            string?                         templateDrivePath = null,
            string?                         switchName        = null,
            IEnumerable<GrpcVirtualDrive>?  extraDrives       = null,
            string?                         notes             = null)
        {
            this.MachineName       = machineName;
            this.MemorySize        = memorySize;
            this.ProcessorCount    = processorCount;
            this.DriveSize         = driveSize;
            this.DrivePath         = drivePath;
            this.CheckpointDrives  = checkpointDrives;
            this.TemplateDrivePath = templateDrivePath;
            this.SwitchName        = switchName;
            this.ExtraDrives       = extraDrives?.ToList();
            this.Notes             = notes;
        }

        /// <summary>
        /// Specifies the machine name.
        /// </summary>
        [DataMember(Order = 1)]
        public string? MachineName { get; set; }

        /// <summary>
        /// Optionally specifies the memory size.  This can be a long byte count or a
        /// a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2GiB</b>, 
        /// or <b>1TiB</b>.  This defaults to <b>2GiB</b>.
        /// </summary>
        [DataMember(Order = 2)]
        public string? MemorySize { get; set; }

        /// <summary>
        /// Optionally specifies the number of virutal processors to assign to the machine.
        /// This defaults to 4.
        /// </summary>
        [DataMember(Order = 3)]
        public int? ProcessorCount { get; set; }

        /// <summary>
        /// Optionally specifies the primary disk size.  This can be a long byte count or
        /// a number with units like <b>512MB</b>, <b>0.5GiB</b>, <b>2GiB</b>, or <b>1TiB</b>.  
        /// Pass <c>null</c> to leave the disk alone.  This defaults to <c>null</c>.
        /// </summary>
        [DataMember(Order = 4)]
        public string? DriveSize { get; set; }

        /// <summary>
        /// Optionally specifies the path where the virtual hard drive will be located.  Pass 
        /// <c>null</c> or empty to default to <b>MACHINE-NAME.vhdx</b> located in the default
        /// Hyper-V virtual machine drive folder.
        /// </summary>
        [DataMember(Order = 5)]
        public string? DrivePath { get; set; }

        /// <summary>
        /// Optionally enables disk checkpoints.  This defaults to <c>false</c>.
        /// </summary>
        [DataMember(Order = 6)]
        public bool CheckpointDrives { get; set; } = false;

        /// <summary>
        /// If this is specified and <see cref="DrivePath"/> is not <c>null</c> then
        /// the hard drive template at <see cref="TemplateDrivePath"/> will be copied
        /// to <see cref="DrivePath"/> before creating the machine.
        /// </summary>
        [DataMember(Order = 7)]
        public string? TemplateDrivePath { get; set; }

        /// <summary>
        /// Optionally specifies the name of the associated virtual switch.
        /// </summary>
        [DataMember(Order = 8)]
        public string? SwitchName { get; set; }

        /// <summary>
        /// Optionally specifies any additional virtual drives to be created and 
        /// then attached to the new virtual machine.
        /// </summary>
        [DataMember(Order = 9)]
        public List<GrpcVirtualDrive>? ExtraDrives { get; set; }

        /// <summary>
        /// Optionally specifies any VM notes.
        /// </summary>
        [DataMember(Order = 10)]
        public string? Notes { get; set; }
    }
}
