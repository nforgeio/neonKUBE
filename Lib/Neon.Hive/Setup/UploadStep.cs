//-----------------------------------------------------------------------------
// FILE:	    UploadStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Hive
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
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="path">The path on the node for uploaded file.</param>
        /// <param name="text">The text to be uploaded.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies target file permissions (compatible with the <c>chmod</c> command).</param>
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
        /// on a specific Docker node.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="path">The path on the node for uploaded file.</param>
        /// <param name="text">The text to be uploaded.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies target file permissions (compatible with the <c>chmod</c> command).</param>
        private UploadStep(string nodeName, string path, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            this.nodeName       = nodeName;
            this.path           = path;
            this.text           = text;
            this.tabStop        = tabStop;
            this.outputEncoding = outputEncoding;
            this.permissions    = permissions;
        }

        /// <inheritdoc/>
        public override void Run(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            var node   = hive.GetNode(nodeName);
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
