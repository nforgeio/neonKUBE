//-----------------------------------------------------------------------------
// FILE:	    Program.DownloadConstUri.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

using Neon.Common;

namespace NeonBuild
{
    public static partial class Program
    {
        /// <summary>
        /// Implements the <b>download-const-uri</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        public static void DownloadConstUri(CommandLine commandLine)
        {
            commandLine = commandLine.Shift(1);

            if (commandLine.Arguments.Length != 4)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            // We're going to download the file as a byte array and then compare this
            // against any existing file and only update the target file when the source
            // and target differ.  This will avoid writing to disk unnecessarily.

            var assemblyPath = commandLine.Arguments[0];
            var typeName     = commandLine.Arguments[1];
            var constName    = commandLine.Arguments[2];
            var targetPath   = commandLine.Arguments[3];

            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine(commandLine);
            Console.WriteLine();
            Console.WriteLine($"AssemblyPath: {assemblyPath}");
            Console.WriteLine($"TypeName:     {typeName}");
            Console.WriteLine($"ConstName     {constName}");
            Console.WriteLine($"TargetPath:   {targetPath}");

            // Load the referenced assembly and locate the type and extract the constant URI.

            var sourceUri = (string)null;
            var assembly  = Assembly.LoadFrom(assemblyPath);
            var type      = assembly.GetType(typeName);

            if (type == null)
            {
                throw new Exception($"Cannot locate type [{typeName}] within the [{assemblyPath}] assembly.");
            }

            var field = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(field => field.Name == constName && field.IsLiteral)
                .FirstOrDefault();

            if (field == null)
            {
                throw new Exception($"Cannot locate constant [{constName}] within the [{typeName}] type.");
            }

            if (field.FieldType != typeof(string))
            {
                throw new Exception($"Cannot constant [{typeName}.{constName}] is not a [string].");
            }

            sourceUri = (string)field.GetValue(null);

            Console.WriteLine($"SourceUri:    {sourceUri}");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var downloadBytes = httpClient.GetByteArrayAsync(sourceUri).Result;

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                    if (File.Exists(targetPath))
                    {
                        var existingBytes = File.ReadAllBytes(targetPath);

                        if (!NeonHelper.ArrayEquals(downloadBytes, existingBytes))
                        {
                            File.WriteAllBytes(targetPath, downloadBytes);
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(targetPath, downloadBytes);
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Manifest downloaded successfully.");
                Console.WriteLine("-------------------------------------------------------------------------------");
            }
            catch (AggregateException e)
            {
                var innerException     = e.InnerExceptions.First();
                var innerExceptionType = innerException.GetType();

                if (innerExceptionType == typeof(SocketException) ||
                    innerExceptionType == typeof(HttpRequestException))
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"*** WARNING: Unable to download [{sourceUri}]: {NeonHelper.ExceptionError(innerException)}");
                    Console.Error.WriteLine();
                }
                else
                {
                    Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(innerException)}");
                    Program.Exit(1);
                }
            }
            catch (SocketException e)
            {
                Console.Error.WriteLine($"*** WARNING: Unable to download [{targetPath}]: {NeonHelper.ExceptionError(e)}");
            }
            catch (HttpRequestException e)
            {
                Console.Error.WriteLine($"*** WARNING: Unable to download [{targetPath}]: {NeonHelper.ExceptionError(e)}");
            }
            catch (IOException e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Program.Exit(1);
            }
            catch (Exception e)
            {
                // Note that we're not returning non-zero exit codes for non-I/O errors
                // so that developers will be able to build when offline.

                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
            }
        }
    }
}
