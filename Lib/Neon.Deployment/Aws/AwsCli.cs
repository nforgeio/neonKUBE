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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
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
        /// The target S3 URI.  This may be either an <b>s3://BUCKET/KEY</b> or a
        /// <b>https://s3.REGION.amazonaws.com/BUCKET/KEY</b> URI referencing an S3 
        /// bucket and key.
        /// </param>
        /// <param name="gzip">Optionally indicates that the target content encoding should be set to <b>gzip</b>.</param>
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
        /// <param name="publicReadAccess">Optionally grant the upload public read access.</param>
        public static void S3Upload(string sourcePath, string targetUri, bool gzip = false, string metadata = null, bool publicReadAccess = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourcePath), nameof(sourcePath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetUri), nameof(targetUri));

            // $todo(jefflill):
            //
            // Hardcoding [max_concurrent_requests = 5] for now, down from the default value: 10.
            // I believe the higher setting is making uploads less reliable and may also consume
            // too much bandwidth.  We should probably make this a parameter.
            //
            // Note this changes this setting system side.

            ExecuteSafe("configure", "set", "default.s3.max_concurrent_requests", "5");

            // Perform the upload.

            var s3Uri = NetHelper.ToAwsS3Uri(targetUri);
            var args  = new List<string>()
            {
                "s3", "cp", sourcePath, s3Uri
            };

            if (gzip)
            {
                args.Add("--content-encoding");
                args.Add("gzip");
            }

            var sbMetadata = new StringBuilder();

            if (!string.IsNullOrEmpty(metadata) && metadata.Contains('='))
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

            if (publicReadAccess)
            {
                var uri    = new Uri(s3Uri, UriKind.Absolute);
                var bucket = uri.Host;
                var key    = uri.AbsolutePath.Substring(1);

                s3Retry.Invoke(() => ExecuteSafe("s3api", "put-object-acl", "--bucket", bucket, "--key", key, "--acl", "public-read"));
            }
        }

        /// <summary>
        /// Uploads the contents of a stream to an S3 bucket.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="targetUri">
        /// The target S3 URI.  This may be either an <b>s3://BUCKET/KEY</b> or a
        /// <b>https://s3.REGION.amazonaws.com/BUCKET/KEY</b> URI referencing an S3 
        /// bucket and key.
        /// </param>
        /// <param name="gzip">Optionally indicates that the target content encoding should be set to <b>gzip</b>.</param>
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
        /// <param name="publicReadAccess">Optionally grant the upload public read access.</param>
        public static void S3Upload(Stream input, string targetUri, bool gzip = false, string metadata = null, bool publicReadAccess = false)
        {
            Covenant.Assert(input != null, nameof(input));

            using (var tempFile = new TempFile())
            {
                using (var output = new FileStream(tempFile.Path, FileMode.Create, FileAccess.ReadWrite))
                {
                    input.CopyTo(output);
                }

                S3Upload(tempFile.Path, targetUri, gzip: gzip, metadata: metadata, publicReadAccess: publicReadAccess);
            }
        }

        /// <summary>
        /// Uploads text to an S3 bucket.
        /// </summary>
        /// <param name="text">The text being uploaded.</param>
        /// <param name="targetUri">
        /// The target S3 URI.  This may be either an <b>s3://BUCKET/KEY</b> or a
        /// <b>https://s3.REGION.amazonaws.com/BUCKET/KEY</b> URI referencing an S3 
        /// bucket and key.
        /// </param>
        /// <param name="gzip">Optionally indicates that the target content encoding should be set to <b>gzip</b>.</param>
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
        /// <param name="publicReadAccess">Optionally grant the upload public read access.</param>
        /// <param name="encoding">Optionally specifies the text encoding.  This defaults to <see cref="Encoding.UTF8"/>.</param>
        public static void S3UploadText(string text, string targetUri, bool gzip = false, string metadata = null, bool publicReadAccess = false, Encoding encoding = null)
        {
            text     ??= string.Empty;
            encoding ??= Encoding.UTF8;

            using (var tempFile = new TempFile())
            {
                File.WriteAllText(tempFile.Path, text, encoding);

                S3Upload(tempFile.Path, targetUri, gzip: gzip, metadata: metadata, publicReadAccess: publicReadAccess);
            }
        }

        /// <summary>
        /// Uploads a byte array to an S3 bucket.
        /// </summary>
        /// <param name="bytes">The byte array being uploaded.</param>
        /// <param name="targetUri">
        /// The target S3 URI.  This may be either an <b>s3://BUCKET/KEY</b> or a
        /// <b>https://s3.REGION.amazonaws.com/BUCKET/KEY</b> URI referencing an S3 
        /// bucket and key.
        /// </param>
        /// <param name="gzip">Optionally indicates that the target content encoding should be set to <b>gzip</b>.</param>
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
        /// <param name="publicReadAccess">Optionally grant the upload public read access.</param>
        public static void S3UploadBytes(byte[] bytes, string targetUri, bool gzip = false, string metadata = null, bool publicReadAccess = false)
        {
            bytes ??= Array.Empty<byte>();

            using (var tempFile = new TempFile())
            {
                File.WriteAllBytes(tempFile.Path, bytes);
                S3Upload(tempFile.Path, targetUri, gzip: gzip, metadata: metadata, publicReadAccess: publicReadAccess);
            }
        }

        /// <summary>
        /// Downloads a file from S3.
        /// </summary>
        /// <param name="sourceUri">
        /// The target S3 URI.  This may be either an <b>s3://BUCKET/KEY</b> or a
        /// <b>https://s3.REGION.amazonaws.com/BUCKET/KEY</b> URI referencing an S3 
        /// bucket and key.
        /// </param>
        /// <param name="targetPath">The target file path.</param>
        public static void S3Download(string sourceUri, string targetPath)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourceUri), nameof(sourceUri));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetPath), nameof(targetPath));

            s3Retry.Invoke(() => ExecuteSafe("s3", "cp", NetHelper.ToAwsS3Uri(sourceUri), targetPath));
        }

        /// <summary>
        /// Downloads a file from S3 as text.
        /// </summary>
        /// <param name="sourceUri">
        /// The target S3 URI.  This may be either an <b>s3://BUCKET/KEY</b> or a
        /// <b>https://s3.REGION.amazonaws.com/BUCKET/KEY</b> URI referencing an S3 
        /// bucket and key.
        /// </param>
        /// <param name="encoding">Optionally specifies the character encoding.  This defaults to <see cref="Encoding.UTF8"/>.</param>
        public static string S3DownloadText(string sourceUri, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;

            using (var tempFile = new TempFile())
            {
                S3Download(sourceUri, tempFile.Path);

                return File.ReadAllText(tempFile.Path, encoding);
            }
        }

        /// <summary>
        /// Downloads a file from S3 as a byte array.
        /// </summary>
        /// <param name="sourceUri">
        /// The target S3 URI.  This may be either an <b>s3://BUCKET/KEY</b> or a
        /// <b>https://s3.REGION.amazonaws.com/BUCKET/KEY</b> URI referencing an S3 
        /// bucket and key.
        /// </param>
        public static byte[] S3DownloadBytes(string sourceUri)
        {
            using (var tempFile = new TempFile())
            {
                S3Download(sourceUri, tempFile.Path);

                return File.ReadAllBytes(tempFile.Path);
            }
        }

        /// <summary>
        /// <para>
        /// Uploads a file in multiple parts from the local workstation to S3, returning the
        /// <see cref="DownloadManifest"/> details. required by <see cref="DeploymentHelper.DownloadMultiPart(DownloadManifest, string, DownloadProgressDelegate, IRetryPolicy, TimeSpan)"/>
        /// and <see cref="DeploymentHelper.DownloadMultiPartAsync(DownloadManifest, string, DownloadProgressDelegate, TimeSpan, IRetryPolicy, System.Threading.CancellationToken)"/>
        /// to actually download the entire file.  The URI to the uploaded <see cref="DownloadManifest"/> details is also returned.
        /// </para>
        /// <para>
        /// See the remarks for details about how this works.
        /// </para>
        /// </summary>
        /// <param name="sourcePath">Path to the file being uploaded.</param>
        /// <param name="targetFolderUri">
        /// <para>
        /// The target S3 URI structured like <b>https://s3.REGION.amazonaws.com/BUCKET/...</b> 
        /// URI referencing an S3 bucket and the optional folder where the file's download information 
        /// and parts will be uploaded.
        /// </para>
        /// <note>
        /// The <b>s3://</b> URI scheme is not supported.
        /// </note>
        /// </param>
        /// <param name="version">The download version.</param>
        /// <param name="name">Optionally overrides the download file name specified by <paramref name="sourcePath"/> to initialize <see cref="DownloadManifest.Name"/>.</param>
        /// <param name="filename">Optionally overrides the download file name specified by <paramref name="sourcePath"/> to initialize <see cref="DownloadManifest.Filename"/>.</param>
        /// <param name="noMd5File">
        /// This method creates a file named [<paramref name="sourcePath"/>.md5] with the MD5 hash for the entire
        /// uploaded file by default.  You may override this behavior by passing <paramref name="noMd5File"/>=<c>true</c>.
        /// </param>
        /// <param name="maxPartSize">Optionally overrides the maximum part size (defailts to 100 MiB).</param>
        /// <param name="publicReadAccess">Optionally grant the upload public read access.</param>
        /// <returns>The <see cref="DownloadManifest"/> information.</returns>
        /// <returns>The <see cref="DownloadManifest"/> information as well as the URI to the uploaded manifest.</returns>
        /// <remarks>
        /// <para>
        /// This method works by splitting the <paramref name="sourcePath"/> file into parts no larger than 
        /// <paramref name="maxPartSize"/> bytes each and the uploading these parts to the specified bucket
        /// and path along with a file holding <see cref="DownloadManifest"/> information describing the download
        /// and its constituent parts.  This information includes details about the download including the
        /// overall MD5 and size as well records describing each part including their URIs, sizes and MD5.
        /// </para>
        /// <para>
        /// The <see cref="DownloadManifest"/> details returned include all of the information required by
        /// <see cref="DeploymentHelper.DownloadMultiPart(DownloadManifest, string, DownloadProgressDelegate, IRetryPolicy, TimeSpan)"/> and
        /// <see cref="DeploymentHelper.DownloadMultiPartAsync(DownloadManifest, string, DownloadProgressDelegate, TimeSpan, IRetryPolicy, System.Threading.CancellationToken)"/>
        /// to actually download the entire file and the URI returned references these msame details as
        /// uploaded to S3.
        /// </para>
        /// <para>
        /// You'll need to pass <paramref name="sourcePath"/> as the path to the file being uploaded 
        /// and <paramref name="targetFolderUri"/> as the S3 location where the download information and the
        /// file parts will be uploaded.  <paramref name="targetFolderUri"/> may use with the <b>https://</b>
        /// or <b>s3://</b> URI scheme.
        /// </para>
        /// <para>
        /// By default the uploaded file and parts names will be based on the filename part of <paramref name="sourcePath"/>,
        /// but this can be overridden via <paramref name="filename"/>.  The <see cref="DownloadManifest"/> information for the
        /// file will be uploaded as <b>FILENAME.manifest</b> and the parts will be written to a subfolder named
        /// <b>FILENAME.parts</b>.  For example, uploading a large file named <b>myfile.json</b> to <b>https://s3.uswest.amazonaws.com/mybucket</b>
        /// will result S3 file layout like:
        /// </para>
        /// <code>
        /// https://s3.uswest.amazonaws.com/mybucket
        ///     myfile.json.manifest
        ///     myfile.json.parts/
        ///         part-0000
        ///         part-0001
        ///         part-0002
        ///         ...
        /// </code>
        /// <para>
        /// The URI returned in this case will be <b>https://s3.uswest.amazonaws.com/mybucket/myfile.json.manifest</b>.
        /// </para>
        /// </remarks>
        public static (DownloadManifest manifest, string manifestUri) S3UploadMultiPart(
            string      sourcePath, 
            string      targetFolderUri, 
            string      version, 
            string      name             = null, 
            string      filename         = null, 
            bool        noMd5File        = false,
            long        maxPartSize      = (long)(100 * ByteUnits.MebiBytes),
            bool        publicReadAccess = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourcePath), nameof(sourcePath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetFolderUri), nameof(targetFolderUri));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version), nameof(version));

            if (!Uri.TryCreate(targetFolderUri, UriKind.Absolute, out var uriCheck))
            {
                Covenant.Assert(false, $"Invalid [{nameof(targetFolderUri)}={targetFolderUri}].");
            }

            Covenant.Assert(uriCheck.Scheme == "https", $"Invalid scheme in [{nameof(targetFolderUri)}={targetFolderUri}].  Only [https://] is supported.");

            name     = name ?? Path.GetFileName(sourcePath);
            filename = filename ?? Path.GetFileName(sourcePath);

            // Determine the base URI for the download manifest and parts on S3.

            var baseUri = targetFolderUri;

            if (!baseUri.EndsWith('/'))
            {
                baseUri += '/';
            }

            baseUri += filename;

            // Remove any existing manifest object as well as any parts.

            var manifestUri = $"{baseUri}.manifest";
            var partsFolder = $"{baseUri}.parts/";

            S3Remove(manifestUri);
            S3Remove(partsFolder, recursive: true, include: $"{partsFolder}*");

            // We're going to upload the parts first, while initializing the download manifest as we go.

            var manifest = new DownloadManifest() { Name = name, Version = version, Filename = filename };

            using (var input = File.OpenRead(sourcePath))
            {
                var partCount   = NeonHelper.PartitionCount(input.Length, maxPartSize);
                var partNumber  = 0;
                var partStart   = 0L;
                var cbRemaining = input.Length;

                manifest.Md5   = CryptoHelper.ComputeMD5String(input);
                input.Position = 0;

                while (cbRemaining > 0)
                {
                    var partSize = Math.Min(cbRemaining, maxPartSize);
                    var part     = new DownloadPart()
                    {
                        Uri    = $"{partsFolder}part-{partNumber:000#}",
                        Number = partNumber,
                        Size   = partSize,
                    };

                    // We're going to use a substream to compute the MD5 hash for the part
                    // as well as to actually upload the part to S3.

                    using (var partStream = new SubStream(input, partStart, partSize))
                    {
                        part.Md5            = CryptoHelper.ComputeMD5String(partStream);
                        partStream.Position = 0;

                        S3Upload(partStream, part.Uri, publicReadAccess: publicReadAccess);
                    }

                    manifest.Parts.Add(part);

                    // Loop to handle the next part (if any).

                    partNumber++;
                    partStart   += partSize;
                    cbRemaining -= partSize;
                }

                manifest.Size = manifest.Parts.Sum(part => part.Size);
            }

            // Upload the manifest.

            S3UploadText(NeonHelper.JsonSerialize(manifest, Formatting.Indented), manifestUri, metadata: $"Content-Type={DeploymentHelper.DownloadManifestContentType}", publicReadAccess: publicReadAccess);

            // Write the MD5 file unless disabled.

            if (!noMd5File)
            {
                File.WriteAllText($"{sourcePath}.md5", manifest.Md5);
            }

            return (manifest: manifest, manifestUri: manifestUri);
        }

        /// <summary>
        /// Removes one S3 objects.
        /// </summary>
        /// <param name="targetUri">
        /// The target S3 URI or prefix for the object(s) to be removed.  This may be either an
        /// <b>s3://BUCKET[/KEY]</b> or a <b>https://s3.REGION.amazonaws.com/BUCKET[/KEY]</b> URI 
        /// referencing an S3 bucket and key.  Note that the key is optional which means that all
        /// objects in the bucket are eligible for removal.
        /// </param>
        /// <param name="recursive">
        /// Optionally indicates <paramref name="targetUri"/> specifies a folder prefix and that
        /// all objects within the folder are eligble for removal.
        /// </param>
        /// <param name="include">Optionally specifies a pattern specifying the objects to be removed.</param>
        /// <param name="exclude">Optionally specifies a pattern specifying objects to be excluded from removal.</param>
        public static void S3Remove(string targetUri, bool recursive = false, string include = null, string exclude = null)
        {
            var s3Uri = NetHelper.ToAwsS3Uri(targetUri);
            var args  = new List<string>()
            {
                "s3", "rm", s3Uri
            };

            if (recursive)
            {
                args.Add("--recursive");
            }

            if (!string.IsNullOrEmpty(include))
            {
                args.Add("--include");
                args.Add(include);
            }

            if (!string.IsNullOrEmpty(exclude))
            {
                args.Add("--exclude");
                args.Add(exclude);
            }

            s3Retry.Invoke(() => ExecuteSafe(args.ToArray()));
        }
    }
}
