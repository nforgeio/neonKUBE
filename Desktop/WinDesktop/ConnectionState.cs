//-----------------------------------------------------------------------------
// FILE:	    ConnectionState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

using Neon.Common;
using Neon.Kube;
using Neon.Windows;

namespace WinDesktop
{
    /// <summary>
    /// Enumerates the possible cluster connection states.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// No cluster is connected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// A cluster is connected.
        /// </summary>
        Connected,

        /// <summary>
        /// A cluster is supposed to be connected but there's a problem.
        /// </summary>
        Error
    }
}
