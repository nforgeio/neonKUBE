//-----------------------------------------------------------------------------
// FILE:	    UploadStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Text;

namespace Neon.Kube
{
    /// <summary>
    /// Uploads a file.
    /// </summary>
    public class UploadStep : ConfigStep
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Uploads text to a file.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <param name="path">The path on the node for uploaded file.</param>
        /// <param name="text">The text to be uploaded.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies target file permissions (compatible with the <c>chmod</c> command).</param>
        /// <returns>The <see cref="UploadStep"/>.</returns>
        public static UploadStep Text(string nodeName, string path, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null)
        {
            return new UploadStep(nodeName, path, text, tabStop, outputEncoding, permissions);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string      nodeName;
        private string      path;
        private string      text;
        private int         tabStop;
        private Encoding    outputEncoding;
        private string      permissions;

        /// <summary>
        /// Constructs a configuration step that executes a command under <b>sudo</b>
        /// on a specific cluster node.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <param name="path">The path on the node for uploaded file.</param>
        /// <param name="text">The text to be uploaded.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies target file permissions (compatible with the <c>chmod</c> command).</param>
        private UploadStep(string nodeName, string path, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName), nameof(nodeName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            this.nodeName       = nodeName;
            this.path           = path;
            this.text           = text;
            this.tabStop        = tabStop;
            this.outputEncoding = outputEncoding;
            this.permissions    = permissions;
        }

        /// <inheritdoc/>
        public override void Run(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            var node   = cluster.GetNode(nodeName);
            var status = this.ToString();

            node.UploadText(path, text, tabStop, outputEncoding);

            if (!string.IsNullOrEmpty(permissions))
            {
                node.SudoCommand("chmod", permissions, path);
            }

            StatusPause();

            node.Status = string.Empty;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"upload [{path}]";
        }
    }
}
