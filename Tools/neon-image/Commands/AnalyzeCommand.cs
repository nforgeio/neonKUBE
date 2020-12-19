//-----------------------------------------------------------------------------
// FILE:	    AnalyzeCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;

namespace NeonImage
{
    /// <summary>
    /// Implements the <b>analyze</b> command.
    /// </summary>
    public class AnalyzeCommand : CommandBase
    {
        private const string usage = @"
Analyzes the required container images.

USAGE:

    neon-image analyze [--fetch] [ INFO-PATH ]

ARGUMENTS:

    INFO-PATH       - Path to the image information file.  This defaults to
                      [image-info.json] in the current working directory.

OPTIONS:

    --fetch         - Reload the image information from their repositories
                      when the INFO-PATH file exists (this is implied when
                      when the file is missing).

REMARKS:

This 
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "analyze" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        private List<ContainerManifest>     containerManifests;

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var fetch    = commandLine.HasOption("--fetch");
            var infoPath = commandLine.Arguments.ElementAtOrDefault(0);

            if (infoPath == null)
            {
                infoPath = Path.Combine(Directory.GetCurrentDirectory(), "image-info.json");
            }

            await LoadManifestsAsync(infoPath, fetch);
        }

        /// <summary>
        /// Loads the manifests for the container images, fetching these from the
        /// source repositories if necessary.
        /// </summary>
        /// <param name="infoPath">Path to the manifest information file.</param>
        /// <param name="fetch">Pass <c>true</c> to force fetching.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LoadManifestsAsync(string infoPath, bool fetch)
        {
            if (fetch || !File.Exists(infoPath))
            {
                containerManifests = new List<ContainerManifest>();

                foreach (var image in ContainerImages.Required)
                {
                    // We're going to use the experimental [docker manifest inspect] command
                    // which fetches an image manifest from the source repository.

                    Console.WriteLine($"Fetching: {image}");

                    var response = await NeonHelper.ExecuteCaptureAsync("docker.exe",
                        new object[]
                        {
                            "manifest",
                            "inspect",
                            image
                        });

                    response.EnsureSuccess();

                    var manifest = NeonHelper.JsonDeserialize<ContainerManifest>(response.OutputText, strict: false);

                    containerManifests.Add(manifest);
                }

                File.WriteAllText(infoPath, NeonHelper.JsonSerialize(containerManifests, Formatting.Indented));
                return;
            }

            containerManifests = NeonHelper.JsonDeserialize<List<ContainerManifest>>(File.ReadAllText(infoPath), strict: false);
        }
    }
}
