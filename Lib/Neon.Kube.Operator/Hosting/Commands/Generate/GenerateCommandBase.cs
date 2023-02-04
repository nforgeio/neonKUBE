using Neon.Tasks;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.CommandLine;

namespace Neon.Kube.Operator.Generators
{
    internal class GenerateCommandBase : CommandBase
    {
        protected GenerateCommandBase(string name, string description) : base(name, description)
        {
        }

        protected int HandleCommand(string message)
        {
            Console.WriteLine(message);
            return 0;
        }
    }
}
