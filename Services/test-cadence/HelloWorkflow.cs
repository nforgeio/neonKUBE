//------------------------------------------------------------------------------
// FILE:         HelloWorkflow.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Service;
using Neon.Service;

using Test.Neon.Workflows;

namespace CadenceTester
{
    [Workflow(AutoRegister = true)]
    public class HelloWorkflow : WorkflowBase, IHelloWorkflow
    {
        public async Task<string> HelloAsync(string name)
        {
            return await Task.FromResult($"Hello {name}!");
        }
    }
}
