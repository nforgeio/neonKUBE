//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.Service;

using k8s;
using k8s.Models;

namespace NeonSetupHarbor
{
    /// <summary>
    /// The Neon cluster initialization operator.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program entrypoint.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            var service = new Service(NeonServices.SetupHarbor, serviceMap: NeonServiceMap.Production);
            service.AutoTerminateIstioSidecar = true;
            await service.RunAsync();
        }
    }
}
