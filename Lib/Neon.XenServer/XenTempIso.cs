//-----------------------------------------------------------------------------
// FILE:	    XenTempIso.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;

namespace Neon.XenServer
{
    /// <summary>
    /// Holds information about a temporary ISO.  These are created temporarily and 
    /// used during neonKUBE cluster setup to inject a configuration script into a new 
    /// node VM during cluster setup.  See <see cref="XenClient.CreateTempIso(string, string)"/>
    /// for more information.
    /// </summary>
    public class XenTempIso
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public XenTempIso()
        {
        }

        /// <summary>
        /// Returns the UUID for the temporary storage repository holding the ISO.
        /// </summary>
        public string SrUuid { get; internal set; }

        /// <summary>
        /// Returns the UUID for the temporary Physical Block Device (PBD) hosting the
        /// storage repository.
        /// </summary>
        public string PdbUuid { get; internal set; }

        /// <summary>
        /// Returns the UUID for the ISO VDI.
        /// </summary>
        public string VdiUuid { get; internal set; }

        /// <summary>
        /// Returns the name of the CD/DVD that can be insterted into a VM.  This
        /// is currently set to a unique name like <b>neon-dvd-UUID.iso</b> to avoid
        /// conflicts.
        /// </summary>
        public string CdName { get; internal set; }

        /// <summary>
        /// Returns the path on the XenServer for the folder for the storage
        /// repository.
        /// </summary>
        public string SrPath { get; internal set; }
    }
}
