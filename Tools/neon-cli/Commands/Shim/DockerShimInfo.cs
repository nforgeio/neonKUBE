//-----------------------------------------------------------------------------
// FILE:	    DockerShimInfo.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Information returned by the <see cref="ICommand.Shim(DockerShim)"/> methods
    /// with information about whether and how to shim a command to the <b>neon-cli</b>
    /// container.
    /// </summary>
    public class DockerShimInfo
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="shimability">Indicates whether a command can or must be shimmed.</param>
        /// <param name="ensureConnection">
        /// Indicates that the command requires a cluster login before the command is 
        /// executed in a <b>neon-cli</b> container.  This defaults to <c>false</c>.
        /// </param>
        public DockerShimInfo(DockerShimability shimability, bool ensureConnection = false)
        {
            this.Shimability      = shimability;
            this.EnsureConnection = ensureConnection;
        }

        /// <summary>
        /// Indicates whether a command can or must be shimmed.
        /// </summary>
        public DockerShimability Shimability { get; set; }

        /// <summary>
        /// Indicates that the command requires a cluster login before 
        /// the command is executed in a <b>neon-cli</b> container.
        /// </summary>
        public bool EnsureConnection { get; set; }
    }
}
