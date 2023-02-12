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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Kube.Operator.Rbac;

using k8s;

namespace Neon.Kube.Operator.Commands.Generate
{
    internal class GenerateRbacCommand : GenerateCommandBase
    {
        private IServiceProvider serviceProvider { get; set; }

        public GenerateRbacCommand(IServiceProvider serviceProvider) : base("rbac", "Generate RBAC yaml for the operator.")
        {
            Handler = CommandHandler.Create<GenerateRbacCommandArgs>(HandleCommand);

            this.AddOption(new Option<string>(new[] { "--namespace", "-n" })
            {
                Description = "The namespace that the operator is deployed.",
                IsRequired = true
            });

            this.serviceProvider = serviceProvider;
        }

        private int HandleCommand(GenerateRbacCommandArgs args)
        {
            var rbac = new RbacBuilder(serviceProvider, args.Namespace);
            rbac.Build();

            var output = new StringBuilder();

            foreach (var sa in rbac.ServiceAccounts)
            {
                output.AppendLine("---");
                output.AppendLine(KubernetesYaml.Serialize(sa));
            }

            foreach (var cr in rbac.ClusterRoles)
            {
                output.AppendLine("---");
                output.AppendLine(KubernetesYaml.Serialize(cr));
            }

            foreach (var crb in rbac.ClusterRoleBindings)
            {
                output.AppendLine("---");
                output.AppendLine(KubernetesYaml.Serialize(crb));
            }

            foreach (var r in rbac.Roles)
            {
                output.AppendLine("---");
                output.AppendLine(KubernetesYaml.Serialize(r));
            }

            foreach (var rb in rbac.RoleBindings)
            {
                output.AppendLine("---");
                output.AppendLine(KubernetesYaml.Serialize(rb));
            }

            return HandleCommand(output.ToString());
        }

        public class GenerateRbacCommandArgs
        {
            public string Namespace { get; set; }
        }
    }
}
