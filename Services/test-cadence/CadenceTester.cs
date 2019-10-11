//------------------------------------------------------------------------------
// FILE:         CadenceTester.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Service;
using Neon.Service;

namespace CadenceTester
{
    public partial class CadenceTester : KubeService
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map.</param>
        /// <param name="name">The service name.</param>
        public CadenceTester(ServiceMap serviceMap, string name)
            : base(serviceMap, name, ThisAssembly.Git.Branch, ThisAssembly.Git.Commit, ThisAssembly.Git.IsDirty)
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            SetRunning();

            return 0;
        }
    }
}
