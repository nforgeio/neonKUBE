// -----------------------------------------------------------------------------
// FILE:	    DependencyRemoveRepositories.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube.Helm;

namespace HelmMunger
{
    /// <summary>
    /// Implements the <b>dependency remove-repositories</b> command.
    /// </summary>
    [Command]
    public class DependencyRemoveRepositories : CommandBase
    {
        private const string usage = @"
# Removes all [repository] properties from any dependencies within the
# root [Chart.yaml] file within the specified chart folder as well as
# recusively for any subcharts.
#
# NOTE: This only works for [v2] Helm charts; [v1] charts will be ignored.

helm-munger dependency remove-repositories CHART-FOLDER
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "dependency", "remove-repositories" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            var chartFolder = commandLine.Arguments.ElementAtOrDefault<string>(0);

            if (string.IsNullOrEmpty(chartFolder))
            {
                Console.Error.WriteLine($"*** ERROR: Helm chart folder argument is required.");
                Program.Exit(1);
            }

            if (!Directory.Exists(chartFolder))
            {
                Console.Error.WriteLine($"*** ERROR: Helm chart folder [{chartFolder}] does not exist.");
                Program.Exit(1);
            }

            var rootChartPath = Path.Combine(chartFolder, "Chart.yaml");

            if (!File.Exists(rootChartPath))
            {
                Console.Error.WriteLine($"*** ERROR: Cannot find [{rootChartPath}], doesn't look like a Helm chart folder.");
                Program.Exit(1);
            }

            // We're going to scan the Helm chart recursively for [Chart.yaml] files
            // and remove the [repository] properties for all dependencies.

            var chartsChangedCount       = 0;
            var repositoriesRemovedCount = 0;

            foreach (var chartPath in Directory.EnumerateFiles(chartFolder, "Chart.yaml", SearchOption.AllDirectories))
            {
                var chartYaml = await File.ReadAllTextAsync(chartPath);

                if (chartYaml.Contains("apiVersion: v1"))
                {
                    continue;   // Ignore [v1] charts.
                }

                var chart   = NeonHelper.YamlDeserialize<HelmChart>(chartYaml);
                var changed = false;

                if (chart.Dependencies == null)
                {
                    continue;   // Ignore charts without dependencies.
                }

                foreach (var dependency in chart.Dependencies
                    .Where(dependency => dependency.Repository != null)
                    .ToList())
                {
                    dependency.Repository = null;
                    changed               = true;

                    repositoriesRemovedCount++;
                }

                if (changed)
                {
                    await File.WriteAllTextAsync(chartPath, Program.YamlSerializer().Serialize(chart));
                    chartsChangedCount++;
                }
            }

            Console.WriteLine($"[{chartsChangedCount}] charts updated with [{repositoriesRemovedCount}] dependency repositories removed.");
        }
    }
}
