using k8s.Models;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KubeOps.Operator;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Kubernetes operator <see cref="IServiceCollection"/> extension methods.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Kubernetes operator to the service collection.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IOperatorBuilder AddKubernetesOperator(
            this IServiceCollection services)
        {
            return new OperatorBuilder(services).AddOperatorBase();
        }
    }

}
