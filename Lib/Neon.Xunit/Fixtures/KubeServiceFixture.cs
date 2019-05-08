//-----------------------------------------------------------------------------
// FILE:	    KubeServiceFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube.Service;
using Neon.Net;

namespace Neon.Xunit
{
    /// <summary>
    /// Fixture for testing a <see cref="KubeService"/>.
    /// </summary>
    public class KubeServiceFixture<TService> : TestFixture
        where TService : KubeService, new()
    {
        private Task serviceTask;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public KubeServiceFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~KubeServiceFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the service instance.
        /// </summary>
        public TService Service { get; private set; }

        /// <summary>
        /// Starts an instance of a <typeparamref name="TService"/> service.
        /// </summary>
        /// <param name="configurator">Optional callback where you can configure the service instance before it starts.</param>
        public void Start(Action<TService> configurator = null)
        {
            base.CheckDisposed();

            base.Start(
                () =>
                {
                    StartAsComposed(configurator);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="configurator">Optional callback where you can configure the service instance before it starts.</param>
        public void StartAsComposed(Action<TService> configurator = null)
        {
            base.CheckWithinAction();

            if (IsRunning)
            {
                return;
            }

            Service = new TService();
            configurator?.Invoke(Service);
            serviceTask = Service.RunAsync();

            IsRunning = true;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    Reset();
                }

                if (Service != null)
                {
                    StopService();
                    Service.Dispose();
                    Service = null;
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Restarts the service.
        /// </summary>
        /// <param name="configurator">Optional callback where you can configure the service instance before it starts.</param>
        public void Restart(Action<TService> configurator = null)
        {
            Covenant.Requires<InvalidOperationException>(IsRunning);

            StopService();

            Service = new TService();
            configurator?.Invoke(Service);
            serviceTask = Service.RunAsync();
        }

        /// <summary>
        /// Stops the service if it's running.
        /// </summary>
        private void StopService()
        {
            if (Service != null)
            {
                Service.Terminator.Signal();
                serviceTask.Wait();

                Service     = null;
                serviceTask = null;
            }
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            if (!IsDisposed)
            {
                StopService();
            }
        }
    }
}
