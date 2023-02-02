//-----------------------------------------------------------------------------
// FILE:	    ResourceControllerManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Tasks;
using Neon.Kube.Operator.Builder;

using k8s;
using k8s.Autorest;
using k8s.LeaderElection;
using k8s.Models;
using IdentityModel;
using Neon.Kube.Operator.Cache;

namespace Neon.Kube.Operator.ResourceManager
{
    internal class ResourceControllerManager : IHostedService
    {
        private readonly ILogger logger;

        private ComponentRegister componentRegister;
        private IServiceProvider  serviceProvider;
        
        public ResourceControllerManager(
            ComponentRegister componentRegister,
            IServiceProvider  serviceProvider)
        {
            this.componentRegister = componentRegister;
            this.serviceProvider   = serviceProvider;
            this.logger            = serviceProvider.GetService<ILogger>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await SyncContext.Clear;

            foreach (var resourceManagerType in componentRegister.ResourceManagerRegistrations)
            {
                try
                {
                    var resourceManager = serviceProvider.GetRequiredService(resourceManagerType);

                    var methods = resourceManagerType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public);

                    var startMethod = methods
                        .First(m => m.Name == "StartAsync");

                    var task = (Task)startMethod.Invoke(resourceManager, null);
                    await task;
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                }
            }


            foreach (var ct in componentRegister.ControllerRegistrations)
            {
                using (var scope = serviceProvider.CreateScope())
                {
                    try
                    {
                        (Type controllerType, Type entityType) = ct;

                        logger?.LogInformationEx(() => $"Registering controller [{controllerType.Name}].");

                        var controller = ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);

                        var methods = controllerType
                            .GetMethods(BindingFlags.Static | BindingFlags.Public);

                        var startMethod = methods
                            .FirstOrDefault(m => m.Name == "StartAsync");

                        if (startMethod != null)
                        {
                            var task = (Task)startMethod.Invoke(controller, new object[] { serviceProvider });
                            await task;
                        }

                        logger?.LogInformationEx(() => $"Registered controller [{controllerType.Name}]");
                    }
                    catch (Exception e)
                    {
                        logger?.LogErrorEx(e);
                    }
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await SyncContext.Clear;
        }
    }
}