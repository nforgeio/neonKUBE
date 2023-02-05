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

using YamlDotNet;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Neon.Kube.Operator.Commands.Generate
{
    internal class GenerateRbacCommand : GenerateCommandBase
    {
        private IServiceProvider serviceProvider { get; set; }
        private ISerializer Serializer { get; set; }

        public GenerateRbacCommand(IServiceProvider serviceProvider) : base("rbac", "Generate RBAC yaml for the operator.")
        {
            Handler = CommandHandler.Create(() => HandleCommand());

            this.serviceProvider = serviceProvider;
            Serializer = new SerializerBuilder()
                                        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                        .Build();
        }

        private int HandleCommand()
        {
            var rbac = new RbacBuilder(serviceProvider);
            rbac.Build();

            var output = new StringBuilder();

            foreach (var sa in rbac.ServiceAccounts)
            {
                output.AppendLine("---");
                output.AppendLine(Serializer.Serialize(sa));
            }

            foreach (var cr in rbac.ClusterRoles)
            {
                output.AppendLine("---");
                output.AppendLine(Serializer.Serialize(cr));
            }

            foreach (var crb in rbac.ClusterRoleBindings)
            {
                output.AppendLine("---");
                output.AppendLine(Serializer.Serialize(crb));
            }

            foreach (var r in rbac.Roles)
            {
                output.AppendLine("---");
                output.AppendLine(Serializer.Serialize(r));
            }

            foreach (var rb in rbac.RoleBindings)
            {
                output.AppendLine("---");
                output.AppendLine(Serializer.Serialize(rb));
            }

            return HandleCommand(output.ToString());
        }
    }
}
