//-----------------------------------------------------------------------------
// FILE:	    AwsCli.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

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
        /// <param name="gzip">Optionally indicates that the target content encoding should be set to <b>gzip</b>.</param>
        public static void S3Upload(string sourcePath, string targetUri, bool gzip = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourcePath), nameof(sourcePath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetUri), nameof(targetUri));

            var s3Uri = NetHelper.ToAwsS3Uri(targetUri);

            if (gzip)
            {
                ExecuteSafe("s3", "cp", sourcePath, targetUri, "--content-encoding", "gzip");
            }
            else
            {
                ExecuteSafe("s3", "cp", sourcePath, targetUri);
            }
        }
    }
}
