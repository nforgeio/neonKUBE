//-----------------------------------------------------------------------------
// FILE:	    XenVirtualDisk.cs
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
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.XenServer
{
    /// <summary>
    /// Specifies virtual disk creation parameters.
    /// </summary>
    public class XenVirtualDisk
    {
        /// <summary>
        /// Optionally specifies the disk name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optionally specifies the disk description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The disk size in bytes.
        /// </summary>
        public decimal Size { get; set; }

        /// <summary>
        /// Identifies the storage repository where the disk will be
        /// created.  This defaults to <b>"Local storage"</b> indicating
        /// that the disk will be created on the XenServer host's local 
        /// file system.
        /// </summary>
        public string StorageRepository { get; set; } = "Local storage";
    }
}
