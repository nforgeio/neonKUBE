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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;
using Neon.Retry;

namespace Neon.Deployment
{
    /// <summary>
    /// <para>
    /// Wraps the AWS-CLI with methods for common operations.
    /// </para>
    /// <note>
    /// The class methods require that the <b>AWS_ACCESS_KEY_ID</b> and <b>AWS_SECRET_ACCESS_KEY</b>
    /// environment variables be already set with the required AWS credentials.
    /// </note>
    /// </summary>
    public static class AwsCli
    {
        private static IRetryPolicy s3Retry = new ExponentialRetryPolicy(IsS3Transient, maxAttempts: 5, initialRetryInterval: TimeSpan.FromSeconds(15), maxRetryInterval: TimeSpan.FromMinutes(5));

        /// <summary>
        /// Used to detect transient exceptions.
        /// </summary>
        /// <param name="e">The potential transient exceptiopn.</param>
        /// <returns><c>true</c> when the exception is transient.</returns>
        private static bool IsS3Transient(Exception e)
        {
            if (e is ExecuteException executeException)
            {
                Console.Error.WriteLine($"FAIL: exitcode={executeException.ExitCode}");
                Console.Error.WriteLine("---------------------------------------");
                Console.Error.WriteLine("STDOUT:");
                Console.Error.WriteLine(executeException.OutputText);
                Console.Error.WriteLine("STDERR:");
                Console.Error.WriteLine(executeException.ErrorText);

                // $todo(jefflill):
                //
                // It would be nice to identify authentication errors because those
                // won't be transient.  I'm hoping one of the output streams will
                // have enough information to determine this.

                return executeException.ExitCode == 1;
            }

            return false;
        }

        /// <summary>
        /// Executes an AWS-CLI command.
        /// </summary>
        /// <param name="args">The command and arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/> with the exit status and command output.</returns>
        private static ExecuteResponse Execute(params string[] args)
        {
            args = args ?? Array.Empty<string>();

            // It looks like we're getting throttled by AWS when uploading very
            // large (~4GB) files to S3 so we're going to add command line options
            // to allow the CLI to wait longer for connections and read bytes and
            // we're also going to configure adaptive retry mode via environment
            // variables.

            var argsCopy = new List<string>();

            foreach (var arg in args)
            {
                argsCopy.Add(arg);
            }

            argsCopy.Add("--cli-read-timeout"); argsCopy.Add("120");
            argsCopy.Add("--cli-connect-timeout");  argsCopy.Add("120");

            // Copy the current environment variables into a new dictionary.

            var environment = new Dictionary<string, string>();

            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                environment[(string)variable.Key] = (string)variable.Value;
            }

            // Configure the AWS retry mode.

            environment["AWS_RETRY_MODE"]   = "adaptive";
            environment["AWS_MAX_ATTEMPTS"] = "5";

            // Execute the command

            return NeonHelper.ExecuteCapture("aws.exe", argsCopy.ToArray(), environmentVariables: environment);
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
        /// Used to add the <b>--debug</b> option to the AWS CLI command line arguments
        /// when debugging.
        /// </summary>
        /// <param name="args">The argument list.</param>
        /// <returns>The argument list with the debug option, when enabled.</returns>
        private static List<string> AddDebugOption(List<string> args)
        {
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            // args.Add("--debug");

            return args;
        }

        /// <summary>
        /// Sets the AWS credential environment variables by loading them from 1Password.
        /// </summary>
        /// <param name="awsAccessKeyId">Optionally overrides the AWS access key ID 1Password secret name.</param>
        /// <param name="awsSecretAccessKey">Optionally overrides the AWS access key 1Password secret name.</param>
        /// <param name="vault">Optionally overrides the current user's 1Password vault.</param>
        /// <param name="masterPassword">Optionally specifies the master 1Password.</param>
        public static void SetCredentials(string awsAccessKeyId = "AWS_ACCESS_KEY_ID", string awsSecretAccessKey = "AWS_SECRET_ACCESS_KEY", string vault = null, string masterPassword = null)
        {
            var profileClient = new ProfileClient();

            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", profileClient.GetSecretPassword(awsAccessKeyId, vault, masterPassword));
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", profileClient.GetSecretPassword(awsSecretAccessKey, vault, masterPassword));
        }

        /// <summary>
        /// Removes the AWS credential environment variables.
        /// </summary>
        public static void RemoveCredentials()
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
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

            // $todo(jefflill):
            //
            // Hardcoding [max_concurrent_requests = 5] for now, down from the default value: 10.
            // I believe the higher setting is making uploads less reliable and may also consume
            // too much bandwidth.  We should probebly make this a parameter.
            //
            // Note this changes this setting system side.

            ExecuteSafe("configure", "set", "default.s3.max_concurrent_requests", "5");

            // Perform the upload.

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

                    if (value == string.Empty)
                    {
                        // Ignore metadata with empty values.

                        continue;
                    }

                    if (name.StartsWith(customPrefix))
                    {
                        name = name.Substring(customPrefix.Length);

                        metadata = $"{name}={value}";
                    }

                    // Some headers are considered to be "system defined" and need to be 
                    // passed as command line options.  We'll separate those out, add the
                    // necessary options to the arguments and add any remaining headers
                    // to the metadata builder.

                    switch (name.ToLowerInvariant())
                    {
                        case "content-type":
                        case "cache-control":
                        case "content-disposition":
                        case "content-encoding":
                        case "content-language":
                        case "expires":

                            args.Add("--" + name.ToLowerInvariant());
                            args.Add(value);
                            break;

                        default:

                            sbMetadata.AppendWithSeparator($"{name}={value}", ",");
                            break;
                    }

                }
            }

            if (sbMetadata.Length > 0)
            {
                args.Add("--metadata");
                args.Add(sbMetadata.ToString());
            }

            AddDebugOption(args);

            s3Retry.Invoke(() => ExecuteSafe(args.ToArray()));
        }

        /// <summary>
        /// Downloads a file from S3.
        /// </summary>
        /// <param name="sourceUri">
        /// The source S3 URI.  This may be either an <b>s3://...</b> or 
        /// <b>https://...</b> URI that references to an S3 bucket.=
        /// </param>
        /// <param name="targetPath">The target file path.</param>
        public static void S3Download(string sourceUri, string targetPath)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourceUri), nameof(sourceUri));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetPath), nameof(targetPath));

            s3Retry.Invoke(() => ExecuteSafe("s3", "cp", NetHelper.ToAwsS3Uri(sourceUri), targetPath));
        }
    }
}
