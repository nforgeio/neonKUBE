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
using Newtonsoft.Json.Linq;

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

    neon-image analyze [ OPTIONS ] [ INFO-DIR ]

ARGUMENTS:

    INFO-DIR        - Path to the image information directory.  This defaults to
                      [C:\Neon-Containers].

OPTIONS:

    --pull          - Pulls the container images from their repositories to
                      local Docker image cache.  This implies [--extract].

    --extract       - Saves each container image to the information directory
                      and extracts the manifest and layer files there.

REMARKS:

$todo(jefflill): WRITE SOMETHING HERE!!!
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "analyze" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--pull", "--extract" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <summary>
        /// Path to the 7-zip command line executable.
        /// </summary>
        private const string SevenZipPath = @"C:\Program Files\7-Zip\7z.exe";

        /// <summary>
        /// Path to the image information folder.
        /// </summary>
        private string ImageDataFolder;

        /// <summary>
        /// Maps an image name to the image information.
        /// </summary>
        private Dictionary<string, ImageInfo> nameToImage;

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var pull    = commandLine.HasOption("--pull");
            var extract = pull || commandLine.HasOption("--extract");
            
            ImageDataFolder = commandLine.Arguments.ElementAtOrDefault(0);

            if (ImageDataFolder == null)
            {
                ImageDataFolder = @"C:\Neon-Containers";
            }

            if (pull)
            {
                Console.WriteLine("Pulling images:");
                Console.WriteLine();
                Program.PullImages();
                Console.WriteLine();
            }

            if (extract || !Directory.Exists(ImageDataFolder))
            {
                // Clear any old extracted container information.

                NeonHelper.DeleteFolderContents(ImageDataFolder);

                // Save all of the required images to the info folder.

                Console.WriteLine("Extracting images:");
                Console.WriteLine();

                foreach (var imageName in ContainerImages.Required)
                {
                    Console.WriteLine($"Extracting: {imageName}");

                    var imageFolder = GetImageFolder(imageName);
                    var imageTar    = Path.Combine(imageFolder, "image.tar");

                    Directory.CreateDirectory(imageFolder);

                    // Save the image from the local docker cache as a TAR file.

                    var response = await NeonHelper.ExecuteCaptureAsync("docker.exe",
                        new object[]
                        {
                            "save",
                            "--output", imageTar,
                            imageName
                        });

                    response.EnsureSuccess();

                    // Write the image name to the [name.txt] file.

                    File.WriteAllText(Path.Combine(imageFolder, "name.txt"), imageName);

                    // Untar the image to obtain the layers and other information.

                    await ExtractTarAsync(imageTar, imageFolder);
                }

                Console.WriteLine();
            }

            // Process the extracted images to build the [nameToImage] dictionary.

            nameToImage = new Dictionary<string, ImageInfo>();

            foreach (var imageFolder in Directory.GetDirectories(ImageDataFolder))
            {
                var imageName = File.ReadAllText(Path.Combine(imageFolder, "name.txt")).Trim();
                var image     = new ImageInfo(imageName);

                foreach (var layerFolder in Directory.GetDirectories(imageFolder))
                {
                    var layerId        = Path.GetFileName(layerFolder);
                    var layerParentId  = (string)null;
                    var compressedSize = 0L;
                    var layerJson      = JObject.Parse(File.ReadAllText(Path.Combine(layerFolder, "json")));

                    if (!layerJson.TryGetValue<string>("parent", out layerParentId))
                    {
                        layerParentId = (string)null;
                    }

                    using (var stream = new FileStream(Path.Combine(layerFolder, "layer.tar"), FileMode.Open))
                    {
                        compressedSize = stream.Length;
                    }

                    image.IdToLayer.Add(layerId,
                        new LayerInfo()
                        {
                            Id             = layerId,
                            ParentId       = layerParentId,
                            CompressedSize = compressedSize
                        }); ;
                }

                nameToImage.Add(imageName, image);
            }

            // Determine which image layers are shared by at least two of the required
            // container images and then update the property in the container image layers.

            var allLayers = new Dictionary<string, LayerInfo>();

            foreach (var image in nameToImage.Values)
            {
                foreach (var layer in image.IdToLayer.Values)
                {
                    if (allLayers.TryGetValue(layer.Id, out var existingLayer))
                    {
                        existingLayer.IsShared = true;
                    }
                    else
                    {
                        allLayers.Add(layer.Id, layer);
                    }
                }
            }

            foreach (var image in nameToImage.Values)
            {
                foreach (var layer in image.IdToLayer.Values.ToArray())
                {
                    image.IdToLayer[layer.Id] = allLayers[layer.Id];
                }
            }

            // Summarize the images and layers.

            Console.WriteLine("Image Summary");
            Console.WriteLine("-------------");
            Console.WriteLine($"Image Count:    {nameToImage.Count,4:N0}");
            Console.WriteLine($"Total Size:     {allLayers.Values.Sum(layer => layer.CompressedSize) / ByteUnits.MebiBytes,4:N0} MiB");
            Console.WriteLine($"Shared Layers:  {allLayers.Values.Count(layer => layer.IsShared),6}      {allLayers.Values.Where(layer => layer.IsShared).Sum(layer => layer.CompressedSize) / ByteUnits.MebiBytes,4:N0} MiB");
            Console.WriteLine($"Unique Layers:  {allLayers.Values.Count(layer => !layer.IsShared),6}      {allLayers.Values.Where(layer => !layer.IsShared).Sum(layer => layer.CompressedSize) / ByteUnits.MebiBytes,4:N0} MiB");
            Console.WriteLine($"Unique Roots:   {allLayers.Values.Count(layer => layer.IsRoot && !layer.IsShared),6}      {allLayers.Values.Where(layer => layer.IsRoot && !layer.IsShared).Sum(layer => layer.CompressedSize) / ByteUnits.MebiBytes,4:N0} MiB");
            Console.WriteLine();

            // List the images in decending order by size including the size of the first 5 layers along with an 
            // indication that the image layer is shared.

            Console.WriteLine("Images");
            Console.WriteLine("------");

            foreach (var image in nameToImage.Values.OrderByDescending(image => image.CompressedSize))
            {
                Console.WriteLine($"{image.CompressedSize / ByteUnits.MebiBytes,0:N0} - {image.Name}");

                if (image.Layers.Count == 1)
                {
                    Console.Write($"       1 layer: ");
                }
                else
                {
                    Console.Write($"       {image.Layers.Count} layers: ");
                }

                foreach (var layer in image.Layers)
                {
                    Console.Write($"{layer.CompressedSize / ByteUnits.MebiBytes,5:N3}");

                    if (layer.IsShared)
                    {
                        Console.Write("*");
                    }

                    Console.Write(" ");
                }

                Console.WriteLine();
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Returns the folder path where the data for the referenced image will be stored.
        /// This is the image name with any forward slashes converted to a "+" and the 
        /// colon separating the image tag converted to "@".
        /// </summary>
        /// <param name="imageRef">The image reference including the name and tag.</param>
        /// <returns>The image directory path.</returns>
        private string GetImageFolder(string imageRef)
        {
            imageRef = imageRef.Replace('/', '+');
            imageRef = imageRef.Replace(':', '@');

            return Path.Combine(ImageDataFolder, imageRef);
        }

        /// <summary>
        /// Extracts a TAR file to the specified folder.
        /// </summary>
        /// <param name="tarPath">Path to the TAR file.</param>
        /// <param name="folder">Path to the target folder.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExtractTarAsync(string tarPath, string folder)
        {
            var orgDirectory = Environment.CurrentDirectory;

            Directory.CreateDirectory(folder);
            Environment.CurrentDirectory = folder;

            try
            {
                var response = await NeonHelper.ExecuteCaptureAsync(SevenZipPath,
                    new object[]
                    {
                        "e",        // Extract
                        tarPath,
                        "-y",       // Answer YES for overwrite prompts
                        "-spf"      // Full paths
                    });

                response.EnsureSuccess();
            }
            finally
            {
                Environment.CurrentDirectory = orgDirectory;
            }
        }
    }
}
