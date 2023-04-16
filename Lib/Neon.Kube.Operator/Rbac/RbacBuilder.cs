//-----------------------------------------------------------------------------
// FILE:	    RbacBuilder.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.CommandLine.NamingConventionBinder;
using System.CommandLine;
using System.Linq;
using System.Reflection;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;

using Neon.Kube.Resources.CertManager;
using Neon.Kube.Operator.Builder;
using Neon.Kube.Operator.ResourceManager;
using System.Diagnostics.Contracts;
using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Webhook;

namespace Neon.Kube.Operator.Rbac
{
    /// <summary>
    /// Handles building of RBAC rules.
    /// </summary>
    internal class RbacBuilder
    {
        private IServiceProvider        serviceProvider;
        private OperatorSettings        operatorSettings;
        private ComponentRegistration   componentRegistration;
        private string                  @namespace;
        private List<Type>              assemblyTypes;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceProvider">Specifies the the depedency injection service provider.</param>
        /// <param name="namespace">Optionally specifies a custom namespace (defauilts to <b>"default"</b>.</param>
        public RbacBuilder(IServiceProvider serviceProvider, string @namespace = "default") 
        {
            Covenant.Requires<ArgumentNullException>(serviceProvider != null, nameof(serviceProvider));

            this.serviceProvider        = serviceProvider;
            this.@namespace             = @namespace;
            this.operatorSettings       = serviceProvider.GetRequiredService<OperatorSettings>();
            this.componentRegistration  = serviceProvider.GetRequiredService<ComponentRegistration>();
            this.ServiceAccounts        = new List<V1ServiceAccount>();
            this.ClusterRoles           = new List<V1ClusterRole>();
            this.ClusterRoleBindings    = new List<V1ClusterRoleBinding>();
            this.Roles                  = new List<V1Role>();
            this.RoleBindings           = new List<V1RoleBinding>();
        }

        public RbacBuilder(string assemblyPath, OperatorSettings operatorSettings)
        {
            Covenant.Requires<ArgumentNullException>(assemblyPath != null, nameof(assemblyPath));

            this.@namespace            = operatorSettings.DeployedNamespace;
            this.operatorSettings      = operatorSettings;
            this.componentRegistration = new ComponentRegistration();
            this.ServiceAccounts       = new List<V1ServiceAccount>();
            this.ClusterRoles          = new List<V1ClusterRole>();
            this.ClusterRoleBindings   = new List<V1ClusterRoleBinding>();
            this.Roles                 = new List<V1Role>();
            this.RoleBindings          = new List<V1RoleBinding>();

            var assembly = Assembly.LoadFrom(assemblyPath);

            try
            {
                assemblyTypes = assembly.GetTypes().Where(type => type != null).ToList();
            }
            catch (ReflectionTypeLoadException e) 
            {
                assemblyTypes = e.Types.Where(type => type != null).ToList();
            }

            var types = assemblyTypes
                .Where(type => type.GetInterfaces().Count() > 0
                        && type.GetInterfaces().Any(@interface => @interface.GetCustomAttributes<OperatorComponentAttribute>()
                    .Any())).ToList();

            foreach (var type in types)
            {
                switch (type.GetInterfaces()
                    .Where(@interface => @interface.GetCustomAttributes<OperatorComponentAttribute>()
                    .Any())
                    .Select(@interface => @interface.GetCustomAttribute<OperatorComponentAttribute>())
                    .FirstOrDefault().ComponentType)
                {
                    case OperatorComponentType.Controller:

                        if (type.GetCustomAttribute<ControllerAttribute>()?.Ignore == true)
                        {
                            break;
                        }
                        
                        var interfaces = type.GetInterfaces()
                            .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IResourceController<>)))
                            .Select(@interface => @interface.GenericTypeArguments[0]);

                                    
                        foreach (var @interface in interfaces)
                        {
                            componentRegistration.RegisterController(type, @interface);
                        }

                        break;

                    case OperatorComponentType.Finalizer:

                        if (type.GetCustomAttribute<FinalizerAttribute>()?.Ignore == true)
                        {
                            break;
                        }

                        var finalizerInterfaces = type.GetInterfaces()
                            .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IResourceFinalizer<>)))
                            .Select(@interface => @interface.GenericTypeArguments[0]);

                        foreach (var @interface in finalizerInterfaces)
                        {
                            componentRegistration.RegisterFinalizer(type, @interface);
                        }

                        break;

                    case OperatorComponentType.MutationWebhook:

                        if (type.GetCustomAttribute<MutatingWebhookAttribute>()?.Ignore == true)
                        {
                            break;
                        }

                        var mutatingWebhookInterfaces = type.GetInterfaces()
                            .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IMutatingWebhook<>)))
                            .Select(@interface => @interface.GenericTypeArguments[0]);

                        foreach (var @interface in mutatingWebhookInterfaces)
                        {
                            componentRegistration.RegisterMutatingWebhook(type, @interface);
                        }

                        operatorSettings.hasMutatingWebhooks = true;

                        break;

                    case OperatorComponentType.ValidationWebhook:

                        if (type.GetCustomAttribute<ValidatingWebhookAttribute>()?.Ignore == true)
                        {
                            break;
                        }

                        var validatingWebhookInterfaces = type.GetInterfaces()
                            .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IValidatingWebhook<>)))
                            .Select(@interface => @interface.GenericTypeArguments[0]);

                        foreach (var @interface in validatingWebhookInterfaces)
                        {
                            componentRegistration.RegisterValidatingWebhook(type, @interface);
                        }

                        operatorSettings.hasValidatingWebhooks = true;

                        break;
                }
            }
        }

        /// <summary>
        /// Returns the list of service accounts.
        /// </summary>
        public List<V1ServiceAccount> ServiceAccounts { get; private set; }

        /// <summary>
        /// Returns the list of cluster roles.
        /// </summary>
        public List<V1ClusterRole> ClusterRoles { get; private set; }

        /// <summary>
        /// Returns the list of cluster role bindings.
        /// </summary>
        public List<V1ClusterRoleBinding> ClusterRoleBindings { get; private set; }

        /// <summary>
        /// Returns the list of roles.
        /// </summary>
        public List<V1Role> Roles { get; private set; }

        /// <summary>
        /// Returns the list of role bindings.
        /// </summary>
        public List<V1RoleBinding> RoleBindings { get; private set; }
        
        /// <summary>
        /// Builds the RBAC rules.
        /// </summary>
        public void Build()
        {
            var serviceAccount = new V1ServiceAccount().Initialize();

            serviceAccount.Metadata.Name              = operatorSettings.Name;
            serviceAccount.Metadata.NamespaceProperty = @namespace;

            ServiceAccounts.Add(serviceAccount);

            if (assemblyTypes == null)
            {
                assemblyTypes = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(assembly => assembly.GetTypes()).Where(type => type != null).ToList();
            }

            var attributes = assemblyTypes
                
                .SelectMany(type => type
                            .GetCustomAttributes()
                            .Where(attribute => attribute
                                    
                                    .GetType().IsGenericType 
                                    && attribute.GetType().GetGenericTypeDefinition().IsEquivalentTo(typeof(RbacRuleAttribute<>))
                            )
                )
                .Select(attribute => (IRbacRule)attribute)
                .ToList();

            if (operatorSettings.leaderElectionEnabled)
            {
                attributes.Add(
                    new RbacRule<V1Lease>(
                        verbs: RbacVerb.All,
                        scope: Resources.EntityScope.Cluster));
            }

            if (operatorSettings.manageCustomResourceDefinitions)
            {
                attributes.Add(
                    new RbacRule<V1CustomResourceDefinition>(
                        verbs: RbacVerb.All,
                        scope: Resources.EntityScope.Cluster));
            }
            else
            {
                attributes.Add(
                    new RbacRule<V1CustomResourceDefinition>(
                        verbs: RbacVerb.Get | RbacVerb.List | RbacVerb.Watch,
                        scope: Resources.EntityScope.Cluster));
            }

            if (operatorSettings.hasMutatingWebhooks)
            {
                attributes.Add(
                    new RbacRule<V1MutatingWebhookConfiguration>(
                        verbs: RbacVerb.All,
                        scope: Resources.EntityScope.Cluster));
            }

            if (operatorSettings.hasValidatingWebhooks)
            {
                attributes.Add(
                    new RbacRule<V1ValidatingWebhookConfiguration>(
                        verbs: RbacVerb.All,
                        scope: Resources.EntityScope.Cluster));
            }

            if (operatorSettings.certManagerEnabled)
            {
                attributes.Add(
                    new RbacRule<V1Certificate>(
                        verbs: RbacVerb.All,
                        scope: Resources.EntityScope.Namespaced,
                        namespaces: @namespace));

                attributes.Add(
                    new RbacRule<V1Secret>(
                        verbs: RbacVerb.Watch,
                        scope: Resources.EntityScope.Namespaced,
                        namespaces: @namespace,
                        resourceNames: $"{operatorSettings.Name}-webhook-tls"));
            }

            var clusterRules = attributes.Where(attr => attr.Scope == Resources.EntityScope.Cluster)
                .GroupBy(attr => new
                {
                    ApiGroups     = attr.GetKubernetesEntityAttribute().Group,
                    ResourceNames = attr.ResourceNames?.Split(',').Distinct(),
                    Verbs         = attr.Verbs,
                    //SubResources  = attr.SubResources?.Split(",").Distinct().Select(sr => $"{attr.GetKubernetesEntityAttribute().PluralName}/{sr}") ??
                    //                        new List<string>()
                })
                .Select(
                    group => (
                        Verbs: group.Key.Verbs,
                        ResourceNames: group.Key.ResourceNames,
                        //SubResources: group.Key.SubResources,
                        EntityTypes: group.Select(attr => attr.GetKubernetesEntityAttribute()).ToList(),
                        SubResources: group.SelectMany(attr => (attr.SubResources?.Split(",").Distinct().Select(sr => $"{attr.GetKubernetesEntityAttribute().PluralName}/{sr}")) ?? Array.Empty<string>())
                        ))
                .Select(
                    group => new V1PolicyRule
                    {
                        ApiGroups     = group.EntityTypes.Select(entity => entity.Group).Distinct().OrderBy(x => x).ToList(),
                        Resources     = group.EntityTypes.SelectMany(entity => group.SubResources.Append(entity.PluralName)).Distinct().OrderBy(x => x).ToList(),
                        ResourceNames = group.ResourceNames?.Count() > 0 ? group.ResourceNames.OrderBy(x => x).ToList() : null,
                        Verbs         = group.Verbs.ToStrings(),
                    });

            if (clusterRules.Any())
            {
                var clusterRole = new V1ClusterRole().Initialize();

                clusterRole.Metadata.Name = operatorSettings.Name;
                clusterRole.Rules         = clusterRules.ToList();

                ClusterRoles.Add(clusterRole);

                var clusterRoleBinding = new V1ClusterRoleBinding().Initialize();

                clusterRoleBinding.Metadata.Name = operatorSettings.Name;
                clusterRoleBinding.RoleRef       = new V1RoleRef(name: clusterRole.Metadata.Name, apiGroup: "rbac.authorization.k8s.io", kind: "ClusterRole");
                clusterRoleBinding.Subjects      = new List<V1Subject>()
                {
                    new V1Subject(kind: "ServiceAccount", name: operatorSettings.Name, namespaceProperty: serviceAccount.Namespace())
                };

                ClusterRoleBindings.Add(clusterRoleBinding);
            }

            var namespaceRules = new Dictionary<string, List<V1PolicyRule>>();

            namespaceRules[@namespace] = attributes.Where(attr =>
                attr.Scope == Resources.EntityScope.Namespaced)
                    .GroupBy(
                        attr => new
                        {
                            ResourceNames = attr.ResourceNames?.Split(',').Distinct(),
                            Verbs         = attr.Verbs
                        })
                    .Select(
                        group => (
                            Verbs:         group.Key.Verbs,
                            ResourceNames: group.Key.ResourceNames,
                            EntityTypes:   group.Select(attr => attr.GetKubernetesEntityAttribute()).ToList()))
                    .Select(
                        group => new V1PolicyRule
                        {
                            ApiGroups     = group.EntityTypes.Select(entity => entity.Group).Distinct().ToList(),
                            Resources     = group.EntityTypes.Select(entity => entity.PluralName).Distinct().ToList(),
                            ResourceNames = group.ResourceNames?.Count() > 0 ? group.ResourceNames.ToList() : null,
                            Verbs         = group.Verbs.ToStrings(),
                        }).ToList();

            foreach (var registration in componentRegistration.ResourceManagerRegistrations)
            {
                var resourceManager = (IResourceManager)serviceProvider.GetRequiredService(registration);
                var options         = resourceManager.Options();

                var namespaces = options.RbacRules
                    .Where(r => r.NamespaceList() != null)
                    .SelectMany(r => r.NamespaceList())
                    .Distinct()
                    .ToList();

                foreach (var @namespace in namespaces)
                {
                    namespaceRules[@namespace] = options.RbacRules
                        .Select(rule => (IRbacRule)rule)
                        .Where(attr =>
                            attr.Scope == Resources.EntityScope.Namespaced
                            && attr.NamespaceList().Contains(@namespace))
                        .GroupBy(
                            attr => new
                            {
                                ResourceNames = attr.ResourceNames?.Split(',').Distinct(),
                                Verbs         = attr.Verbs,
                            })
                        .Select(
                            group => (
                                Verbs:         group.Key.Verbs,
                                ResourceNames: group.Key.ResourceNames,
                                EntityTypes:   group.Select(attr => attr.GetKubernetesEntityAttribute()).ToList()))
                        .Select(
                            group => new V1PolicyRule
                            {
                                ApiGroups     = group.EntityTypes.Select(entity => entity.Group).Distinct().ToList(),
                                Resources     = group.EntityTypes.Select(entity => entity.PluralName).Distinct().ToList(),
                                ResourceNames = group.ResourceNames?.Count() > 0 ? group.ResourceNames.ToList() : null,
                                Verbs         = group.Verbs.ToStrings(),
                            })
                        .ToList();
                }
            }

            if (namespaceRules.Keys.Any())
            {
                foreach (var @namespace in namespaceRules.Keys)
                {
                    var namespacedRole = new V1Role().Initialize();

                    namespacedRole.Metadata.Name              = operatorSettings.Name;
                    namespacedRole.Metadata.NamespaceProperty = @namespace;
                    namespacedRole.Rules                      = namespaceRules[@namespace].ToList();

                    Roles.Add(namespacedRole);

                    var roleBinding = new V1RoleBinding().Initialize();

                    roleBinding.Metadata.Name              = operatorSettings.Name;
                    roleBinding.Metadata.NamespaceProperty = @namespace;
                    roleBinding.RoleRef                    = new V1RoleRef(name: namespacedRole.Metadata.Name, apiGroup: "rbac.authorization.k8s.io", kind: "Role");
                    roleBinding.Subjects                   = new List<V1Subject>()
                    {
                        new V1Subject(kind: "ServiceAccount", name: operatorSettings.Name, namespaceProperty: serviceAccount.Namespace())
                    };

                    RoleBindings.Add(roleBinding);
                }
            }
        }
    }
}
