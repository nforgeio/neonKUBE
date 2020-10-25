//-----------------------------------------------------------------------------
// FILE:	    IXenClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Used internally by cluster as a potentially temporary
    /// hack required by <see cref="SetupController{NodeMetadata}"/> to display XenServer
    /// provisioning status.  This may be removed at some point in the future.
    /// </summary>
    public interface IXenClient
    {
        /// <summary>
        /// Returns the name of the connected XenServer.
        /// </summary>
        string Name { get; }
    }
}
