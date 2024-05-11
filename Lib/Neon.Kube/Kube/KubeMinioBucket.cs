//-----------------------------------------------------------------------------
// FILE:        KubeMinioBuckets.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.SSH;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// Defines the Minio bucket names used by NeonKUBE applications.
    /// </summary>
    public static class KubeMinioBucket
    {
        /// <summary>
        /// Mimir bucket name.
        /// </summary>
        public const string Mimir = "mimir-tsdb";

        /// <summary>
        /// Mimir-ruler bucket name.
        /// </summary>
        public const string MimirRuler = "mimir-ruler";

        /// <summary>
        /// Harbor bucket name.
        /// </summary>
        public const string Grafana = "grafana";

        /// <summary>
        /// Harbor bucket name.
        /// </summary>
        public const string Harbor = "harbor";

        /// <summary>
        /// Loki bucket name.
        /// </summary>
        public const string Loki = "loki";

        /// <summary>
        /// Tempo bucket name.
        /// </summary>
        public const string Tempo = "tempo";

        /// <summary>
        /// Returns the list of all internal NeonKUBE Minio bucket names.
        /// </summary>
        public static readonly IReadOnlyList<string> All =
            new List<string>()
            {
                Mimir,
                MimirRuler,
                Harbor,
                Loki,
                Tempo
            }
            .AsReadOnly();
    }
}
