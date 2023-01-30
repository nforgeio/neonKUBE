using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Retry;
using Neon.Tasks;

namespace OperatorTemplate
{
    public class  : IOperatorController<V1ExampleEntity>
    {
        private static readonly ILogger logger = TelemetryHub.CreateLogger<ExampleController>();

    /// <summary>
    /// Starts the controller.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
    /// <returns>The tracking <see cref="Task"/>.</returns>
    public static async Task StartAsync(IServiceProvider serviceProvider)
    {
        logger.LogInformationEx(() => $"Starting {nameof(ExampleController)}");
    }

    //---------------------------------------------------------------------
    // Instance members

    private readonly IKubernetes k8s;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ExampleController(IKubernetes k8s)
    {
        Covenant.Requires(k8s != null, nameof(k8s));

        this.k8s = k8s;
    }

    /// <inheritdoc/>
    public async Task IdleAsync()
    {
        logger.LogInformationEx(() => $"IDLE");

        return;
    }

    /// <inheritdoc/>
    public async Task<ResourceControllerResult> ReconcileAsync(V1NeonContainerRegistry resource)
    {
        await SyncContext.Clear;

        logger.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

        return null;
    }

    /// <inheritdoc/>
    public async Task DeletedAsync(V1NeonContainerRegistry resource)
    {
        await SyncContext.Clear;

        logger.LogInformationEx(() => $"DELETED: {resource.Name()}");
    }
}
}
