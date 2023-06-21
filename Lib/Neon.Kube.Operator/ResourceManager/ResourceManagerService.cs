//-----------------------------------------------------------------------------
// FILE:        ResourceControllerManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator.Builder;
using Neon.Kube.Operator.Controller;
using Neon.Tasks;

namespace Neon.Kube.Operator.ResourceManager
{
    /// <summary>
    /// Manages a resource controller.
    /// </summary>
    internal class ResourceControllerManager : IHostedService
    {
        private readonly ILogger<ResourceControllerManager> logger;
        private ComponentRegister                       componentRegistration;
        private IServiceProvider                            serviceProvider;
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="componentRegistration">Specifies the component registration.</param>
        /// <param name="serviceProvider">Specifies the depdency injection service provider.</param>
        public ResourceControllerManager(
            ComponentRegister   componentRegistration,
            IServiceProvider        serviceProvider)
        {
            Covenant.Requires<ArgumentNullException>(componentRegistration != null, nameof(componentRegistration));
            Covenant.Requires<ArgumentNullException>(serviceProvider != null, nameof(serviceProvider));

            this.componentRegistration = componentRegistration;
            this.serviceProvider       = serviceProvider;
            this.logger                = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<ResourceControllerManager>();
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="cancellationToken">Specifies the cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await SyncContext.Clear;

            foreach (var resourceManagerType in componentRegistration.ResourceManagerRegistrations)
            {
                try
                {
                    var resourceManager = (IResourceManager)serviceProvider.GetRequiredService(resourceManagerType);

                    await resourceManager.StartAsync();
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                }
            }

            foreach (var registration in componentRegistration.ControllerRegistrations)
            {
                using (var scope = serviceProvider.CreateScope())
                {
                    try
                    {
                        (Type controllerType, Type entityType) = registration;

                        logger?.LogInformationEx(() => $"Registering controller [{controllerType.Name}].");

                        var controller = (IResourceController)ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);

                        await controller.StartAsync(serviceProvider);

                        logger?.LogInformationEx(() => $"Registered controller [{controllerType.Name}]");
                    }
                    catch (Exception e)
                    {
                        logger?.LogErrorEx(e);
                    }
                }
            }
        }

        /// <summary>
        /// Stops the controller.
        /// </summary>
        /// <param name="cancellationToken">Specifies the cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await SyncContext.Clear;
        }
    }
}
