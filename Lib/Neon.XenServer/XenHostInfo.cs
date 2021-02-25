//-----------------------------------------------------------------------------
// FILE:	    XenHostInfo.cs
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

namespace Neon.XenServer
{
    /// <summary>
    /// Holds information about a XenServer or XCP-ng host machine.
    /// </summary>
    public class XenHostInfo
    {
        /// <summary>
        /// Indicates the installed edition.  This will be <b>xcp-ng</b> or <b>xenserver</b>.
        /// </summary>
        public string Edition { get; internal set; }

        /// <summary>
        /// Indicates the XenServer/XCP-ng version number.
        /// </summary>
        public SemanticVersion Version { get; internal set; }

        /// <summary>
        /// Holds the raw host parameters.
        /// </summary>
        public IDictionary<string, string> Params { get; internal set; }
    }
}
