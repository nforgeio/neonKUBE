//-----------------------------------------------------------------------------
// FILE:	    ServicePortMode.cs
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
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service port modes.
    /// </summary>
    public enum ServicePortMode
    {
        /// <summary>
        /// Publish service ports to the Docker Swarm ingress mesh network.
        /// </summary>
        [EnumMember(Value = "ingress")]
        Ingress = 0,

        /// <summary>
        /// Publish service ports to the local Docker host network.
        /// </summary>
        [EnumMember(Value = "host")]
        Host
    }
}
