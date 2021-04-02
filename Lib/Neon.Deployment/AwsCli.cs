//-----------------------------------------------------------------------------
// FILE:	    AwsCli.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

namespace Neon.Deployment
{
    /// <summary>
    /// Wraps the AWS-CLI with methods for common operations.
    /// </summary>
    public static class AwsCli
    {
        /// <summary>
        /// Executes an AWS-CLI command.
        /// </summary>
        /// <param name="args">The command and arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/> with the exit status and command output.</returns>
        public static ExecuteResponse Execute(params string[] args)
        {
            return NeonHelper.ExecuteCapture("aws.exe", args ?? Array.Empty<object>());
        }

        /// <summary>
        /// Executes an AWS-CLI command, ensuring that it completed without error.
        /// </summary>
        /// <param name="args">The command and arguments.</param>
        /// <exception cref="ExecuteException">Thrown for command errors.</exception>
        public static void ExecuteSafe(params string[] args)
        {
            Execute(args).EnsureSuccess();
        }

        /// <summary>
        /// Uploads a file from the local workstation to S3.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="targetUri">
        /// The target S3 URI.  This may be either an <b>s3://...</b> or 
        /// <b>https://...</b> URI that references to an S3 bucket.=
        /// </param>
        /// <param name="metadata">
        /// <para>
        /// Optionally specifies HTTP metadata headers to be returned when the object
        /// is downloaded from S3.  This formatted as as comma separated a list of 
        /// key/value pairs like:
        /// </para>
        /// <example>
        /// Content-Type=text,app-version=1.0.0
        /// </example>
        /// <note>
        /// <para>
        /// AWS supports <b>system</b> as well as <b>custom</b> headers.  System headers
        /// include standard HTTP headers such as <b>Content-Type</b> and <b>Content-Encoding</b>.
        /// Custom headers are required to include the <b>x-amz-meta-</b> prefix.
        /// </para>
        /// <para>
        /// You don't need to specify the <b>x-amz-meta-</b> prefix for setting custom 
        /// headers; the AWS-CLI detects custom header names and adds the prefix automatically. 
        /// This method will strip the prefix if present before calling the AWS-CLI to ensure 
        /// the prefix doesn't end up being duplicated.
        /// </para>
        /// </note>
        /// </param>
        /// <param name="gzip">Optionally indicates that the target content encoding should be set to <b>gzip</b>.</param>
        public static void S3Upload(string sourcePath, string targetUri, bool gzip = false, string metadata = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourcePath), nameof(sourcePath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetUri), nameof(targetUri));

            var s3Uri = NetHelper.ToAwsS3Uri(targetUri);
            var args  = new List<string>()
            {
                "s3", "cp", sourcePath, targetUri
            };

            if (gzip)
            {
                args.Add("--content-encoding");
                args.Add("gzip");
            }

            var sbMetadata = new StringBuilder();

            if (metadata != null && metadata.Contains('='))
            {
                foreach (var item in metadata.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    // Strip off the [x-amz-meta-] prefix from the name, if present.
                    // Otherwise, the AWS-CLI will add the prefix again, duplicating it.

                    const string customPrefix = "x-amz-meta-";

                    var fields = item.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);

                    if (fields.Length != 2)
                    {
                        throw new ArgumentException($"Invalid metadata [{metadata}].", nameof(metadata));
                    }

                    var name  = fields[0].Trim();
                    var value = fields[1].Trim();

                    if (name.StartsWith(customPrefix))
                    {
                        name = name.Substring(customPrefix.Length);

                        metadata = $"{name}={value}";
                    }

                    sbMetadata.AppendWithSeparator($"{name}={value}", ",");
                }
            }

            if (sbMetadata.Length > 0)
            {
                args.Add("--metadata");
                args.Add(sbMetadata.ToString());
            }

            ExecuteSafe(args.ToArray());
        }
    }
}
