//-----------------------------------------------------------------------------
// FILE:	    NeonAnnotation.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// Defines the non-node annotations used to tag objects by neonKUBE.
    /// </summary>
    public static class NeonAnnotation
    {
        /// <summary>
        /// <para>
        /// Used by <b>neon-cluster-operator</b> to identify namespaces where the <b>neon-otel-collector</b>
        /// shoule be created.
        /// </para>
        /// <para>
        /// The idea is that telemetry forwarding will be enabled by default but that users can disable
        /// this on a namespace basis by adding this label, with the value set to false.
        /// </para>
        /// </summary>
        public const string OtelCollector = ClusterDefinition.ReservedPrefix + "otel-collector";
    }
}
