﻿//------------------------------------------------------------------------------
// FILE:         BusyworkWorkflow.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Service;
using Neon.Temporal;

using Test.Neon.Models.Temporal;

namespace TemporalService
{
    [Workflow(AutoRegister = true)]
    public class BusyworkWorkflow : WorkflowBase, IBusyworkWorkflow
    {
        /// <inheritdoc/>
        public async Task<string> DoItAsync(int iterations, TimeSpan sleepInterval, string message)
        {
            for (int i = 0; i < iterations; i++)
            {
                await Workflow.SleepAsync(sleepInterval);
            }

            return message;
        }
    }
}
