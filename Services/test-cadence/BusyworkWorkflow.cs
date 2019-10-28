//------------------------------------------------------------------------------
// FILE:         BusyworkWorkflow.cs
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

using Test.Neon.Models.Cadence;

namespace CadenceTester
{
    [Workflow(AutoRegister = true)]
    public class BusyworkWorkflow : WorkflowBase, IBusyworkWorkflow
    {
        /// <inheritdoc/>
        public async Task DoIt(int iterations, TimeSpan sleepInterval)
        {
            for (int i = 0; i < iterations; i++)
            {
                await Workflow.SleepAsync(sleepInterval);
            }
        }
    }
}
