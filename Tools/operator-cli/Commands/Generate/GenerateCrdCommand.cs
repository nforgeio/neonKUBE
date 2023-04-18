//-----------------------------------------------------------------------------
// FILE:	    GenerateCrdsCommand.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using k8s;
using Neon.Kube.Operator;
using System.IO;
using Neon.Kube.Operator.Entities;
using k8s.Models;

namespace OperatorCli.Commands.Generate
{
    internal class GenerateCrdsCommand : GenerateCommandBase
    {
        public GenerateCrdsCommand() : base("crds", "Generate Crds yaml for the operator.")
        {
            Handler = CommandHandler.Create<GenerateCrdsCommandArgs>(HandleCommand);

            this.AddOption(new Option<string>(new[] { "--assembly-path" })
            {
                Description = "The assembly path.",
                IsRequired  = false,
                Name        = "AssemblyPath"
            });

            this.AddOption(new Option<string>(new[] { "--output-path" })
            {
                Description = "The output path.",
                IsRequired  = false,
                Name        = "OutputPath"
            });

            this.AddOption(new Option<string>(new[] { "--deployed-namespace" })
            {
                Description = "The namespace that the operator is deployed.",
                IsRequired  = false,
                Name        = "DeployedNamespace"
            });

            this.AddOption(new Option<string>(new[] { "--name" })
            {
                Description = "The operator name.",
                IsRequired  = false,
                Name        = "Name"
            });
        }

        private async Task<int> HandleCommand(GenerateCrdsCommandArgs args)
        {
            var assemblyPath = args.AssemblyPath.Trim('\'').Trim('"').Trim();
            var outputPath   = args.OutputPath.Trim('\'').Trim('"').Trim();

            Directory.CreateDirectory(outputPath);

            var operatorSettings = new OperatorSettings()
            {
                DeployedNamespace       = args.DeployedNamespace,
                Name                    = args.Name
            };

            var crdGenerator = new CustomResourceGenerator();
            var crds         = await crdGenerator.GetCustomResourcesFromAssemblyAsync(assemblyPath, operatorSettings);

            foreach (var crd in crds)
            {
                File.WriteAllText($"{outputPath}/crd-{crd.Name()}.yaml", KubernetesYaml.Serialize(crd));
            }

            return HandleCommand(KubernetesYaml.SerializeAll(crds));
        }

        public class GenerateCrdsCommandArgs
        {
            public string AssemblyPath { get; set; }
            public string OutputPath { get; set; }
            public string DeployedNamespace { get; set; }
            public string Name { get; set; }
        }
    }
}
