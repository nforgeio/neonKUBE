//-----------------------------------------------------------------------------
// FILE:	    DockerShimFolder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Hive;

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
