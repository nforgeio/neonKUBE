using Neon.Tasks;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.CommandLine.Invocation;
using Neon.Kube.Operator.Generators;

namespace Neon.Kube.Operator.Hosting.Commands.Generate
{
    internal class GenerateCommand : CommandBase
    {
        public GenerateCommand() : base("generate", "Generate RBAC yaml for the operator.")
        {
            Handler = CommandHandler.Create(() => HandleCommand());
        }

        private int HandleCommand()
        {
            return HandleCommand("Generating stuff");
        }
    }
}
