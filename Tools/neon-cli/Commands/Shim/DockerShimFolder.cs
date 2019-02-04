//-----------------------------------------------------------------------------
// FILE:	    DockerShimFolder.cs
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
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Identifies a file system folder that is to be mapped from the client
    /// workstation into the <b>neon-tool</b> Docker container.
    /// </summary>
    public class DockerShimFolder
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="clientFolderPath">The client folder path.</param>
        /// <param name="containerFolderPath">he internal container folder path.</param>
        /// <param name="isReadOnly">
        /// Indicates whether the folder is to be considered to be read-only
        /// (the default).
        /// </param>
        public DockerShimFolder(string clientFolderPath, string containerFolderPath, bool isReadOnly = true)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clientFolderPath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(containerFolderPath));

            this.ClientFolderPath    = clientFolderPath;
            this.ContainerFolderPath = containerFolderPath;
            this.IsReadOnly          = isReadOnly;
        }

        /// <summary>
        /// The client folder path.
        /// </summary>
        public string ClientFolderPath { get; set; }

        /// <summary>
        /// The internal container folder path.
        /// </summary>
        public string ContainerFolderPath { get; set; }

        /// <summary>
        /// Indicates whether the folder is to be considered to be read-only
        /// (the default).
        /// </summary>
        public bool IsReadOnly { get; set; } = true;
    }
}
