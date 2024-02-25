// -----------------------------------------------------------------------------
// FILE:	    DependencyRemoveCommand.cs
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
    /// Implements the <b>dependency remove</b> command.
    /// </summary>'
    [Command]
    public class DependencyRemoveCommand : CommandBase
    {
        private const string usage = @"
# Removes the named dependency from the [Chart.yaml] file in the specified
# chart folder and then removes the [charts/DEPENDENCY] subchart folder.

helm-munger dependency remove CHART-FOLDER DEPENDENCY
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "dependency", "remove" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            var chartFolder    = commandLine.Arguments.ElementAtOrDefault<string>(0);
            var dependencyName = commandLine.Arguments.ElementAtOrDefault<string>(1);

            if (string.IsNullOrEmpty(chartFolder))
            {
                Console.Error.WriteLine($"*** ERROR: Helm CHART-FOLDER argument is required.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(dependencyName))
            {
                Console.Error.WriteLine($"*** ERROR: DEPENDENCY argument is required.");
                Program.Exit(1);
            }

            if (!Directory.Exists(chartFolder))
            {
                Console.Error.WriteLine($"*** ERROR: Helm chart folder [{chartFolder}] does not exist.");
                Program.Exit(1);
            }

            var chartPath = Path.Combine(chartFolder, "Chart.yaml");

            if (!File.Exists(chartPath))
            {
                Console.Error.WriteLine($"*** ERROR: Helm chart folder [{chartFolder}] does not exist.");
                Program.Exit(1);
            }

            var chartYaml  = await File.ReadAllTextAsync(chartPath);
            var chart      = NeonHelper.YamlDeserialize<HelmChart>(chartYaml);
            var dependency = chart.Dependencies.SingleOrDefault(dependency => dependency.Name == dependencyName);

            if (dependency == null)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot find dependency [{dependencyName}] in [{chartPath}].");
                Program.Exit(1);
            }

            chart.Dependencies.Remove(dependency);
            await File.WriteAllTextAsync(chartPath, Program.YamlSerializer().Serialize(chart));
            Console.WriteLine($"[{dependencyName}] dependency removed.");
        }
    }
}
