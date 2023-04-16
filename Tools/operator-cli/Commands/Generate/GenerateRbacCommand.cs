//-----------------------------------------------------------------------------
// FILE:	    GenerateRbacCommand.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Neon.Kube.Operator.Rbac;
using Neon.Kube.Operator;

namespace OperatorCli.Commands.Generate
{
    internal class GenerateRbacCommand : GenerateCommandBase
    {
        public GenerateRbacCommand() : base("rbac", "Generate RBAC yaml for the operator.")
        {
            Handler = CommandHandler.Create<GenerateRbacCommandArgs>(HandleCommand);

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

            this.AddOption(new Option<string>(new[] { "--watch-namespace" })
            {
                Description = "The namespace that the operator should watch in csv format.",
                IsRequired  = false,
                Name        = "WatchNamespace"
            });

            this.AddOption(new Option<string>(new[] { "--name" })
            {
                Description = "The operator name.",
                IsRequired  = false,
                Name        = "Name"
            });

            this.AddOption(new Option<bool>(new[] { "--cert-manager-enabled" })
            {
                Description = "enable cert manager.",
                IsRequired  = false,
                Name        = "CertManagerEnabled"
            });

            this.AddOption(new Option<string>(new[] { "--manage-custom-resource-definitions" })
            {
                Description = "Manage custom resources.",
                IsRequired  = false,
                Name        = "ManageCustomResourceDefinitions"
            });

            this.AddOption(new Option<bool>(new[] { "--assembly-scanning-enabled" })
            {
                Description = "enable assembly scanning.",
                IsRequired  = false,
                Name        = "AssemblyScanningEnabled"
            });
        }

        private int HandleCommand(GenerateRbacCommandArgs args)
        {
            var assemblyPath = args.AssemblyPath.Trim('\'').Trim('"').Trim();
            var outputPath   = args.OutputPath.Trim('\'').Trim('"').Trim();

            Directory.CreateDirectory(outputPath);

            var operatorSettings = new OperatorSettings()
            {
                AssemblyScanningEnabled = args.AssemblyScanningEnabled,
                DeployedNamespace       = args.DeployedNamespace,
                Name                    = args.Name,
                ResourceManagerOptions  = new Neon.Kube.Operator.ResourceManager.ResourceManagerOptions()
                {
                    ManageCustomResourceDefinitions = args.ManageCustomResourceDefinitions
                },
                certManagerEnabled = args.CertManagerEnabled,
                WatchNamespace     = args.WatchNamespace,
                
            };

            var rbac          = new RbacBuilder(assemblyPath, operatorSettings);
            var consoleOutput = new StringBuilder();
            
            rbac.Build();


            foreach (var sa in rbac.ServiceAccounts)
            {
                var saString = KubernetesYaml.Serialize(sa);

                consoleOutput.AppendLine("---");
                consoleOutput.AppendLine(saString);
                File.WriteAllText($"{outputPath}/serviceaccount-{sa.Name()}-{sa.Namespace()}.yaml", saString);
            }

            foreach (var cr in rbac.ClusterRoles)
            {
                var crString = KubernetesYaml.Serialize(cr);

                consoleOutput.AppendLine("---");
                consoleOutput.AppendLine(crString);

                File.WriteAllText($"{outputPath}/clusterrole-{cr.Name()}.yaml", crString);
            }

            foreach (var crb in rbac.ClusterRoleBindings)
            {
                var crbString = KubernetesYaml.Serialize(crb);

                consoleOutput.AppendLine("---");
                consoleOutput.AppendLine(crbString);

                File.WriteAllText($"{outputPath}/clusterrolebinding-{crb.Name()}.yaml", crbString);
            }

            foreach (var r in rbac.Roles)
            {
                var rString = KubernetesYaml.Serialize(r);

                consoleOutput.AppendLine("---");
                consoleOutput.AppendLine(rString);
                File.WriteAllText($"{outputPath}/role-{r.Name()}-{r.Namespace()}.yaml", rString);
            }

            foreach (var rb in rbac.RoleBindings)
            {
                var rbString = KubernetesYaml.Serialize(rb);

                consoleOutput.AppendLine("---");
                consoleOutput.AppendLine(rbString);
                File.WriteAllText($"{outputPath}/rolebinding-{rb.Name()}-{rb.Namespace()}.yaml", rbString);
            }

            return HandleCommand(consoleOutput.ToString());
        }

        public class GenerateRbacCommandArgs
        {
            public string AssemblyPath { get; set; }
            public string OutputPath { get; set; }
            public string Name { get; set; }
            public string WatchNamespace { get; set; }
            public string DeployedNamespace { get; set; }
            public bool CertManagerEnabled { get; set; } = true;
            public bool ManageCustomResourceDefinitions { get; set; } = true;
            public bool AssemblyScanningEnabled { get; set; } = true;
        }
    }
}
