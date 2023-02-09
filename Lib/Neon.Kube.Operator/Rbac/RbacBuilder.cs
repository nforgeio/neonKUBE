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

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;

using k8s;
using k8s.Models;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Neon.Kube.Resources.CertManager;
using Neon.Kube.Operator.Builder;
using Neon.Kube.Operator.ResourceManager;
using System.Text.RegularExpressions;

namespace Neon.Kube.Operator.Rbac
{
    internal class RbacBuilder
    {
        public List<V1ServiceAccount>     ServiceAccounts { get; private set; }
        public List<V1ClusterRole>        ClusterRoles { get; private set; }
        public List<V1ClusterRoleBinding> ClusterRoleBindings { get; private set; }
        public List<V1Role>               Roles { get; private set; }
        public List<V1RoleBinding>        RoleBindings { get; private set; }
        
        private IServiceProvider serviceProvider;
        private OperatorSettings operatorSettings;
        private ComponentRegister componentRegister;

        public RbacBuilder(IServiceProvider serviceProvider) 
        {
            this.serviceProvider     = serviceProvider;
            this.operatorSettings    = serviceProvider.GetRequiredService<OperatorSettings>();
            this.componentRegister   = serviceProvider.GetRequiredService<ComponentRegister>();
            this.ServiceAccounts     = new List<V1ServiceAccount>();
            this.ClusterRoles        = new List<V1ClusterRole>();
            this.ClusterRoleBindings = new List<V1ClusterRoleBinding>();
            this.Roles               = new List<V1Role>();
            this.RoleBindings        = new List<V1RoleBinding>();
        }

        public void Build()
        {
            var serviceAccount = new V1ServiceAccount().Initialize();
            serviceAccount.Metadata.Name = operatorSettings.Name;
            serviceAccount.Metadata.NamespaceProperty = operatorSettings.deployedNamespace;

            ServiceAccounts.Add(serviceAccount);

            var attributes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .SelectMany(
                    t => t.GetCustomAttributes()
                    .Where(a => a.GetType().IsGenericType)
                    .Where(a => a.GetType().GetGenericTypeDefinition().IsEquivalentTo(typeof(RbacRuleAttribute<>))))
                .Select(a => (IRbacRule)a)
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
                            scope: Resources.EntityScope.Cluster
                            ));
            }
            else
            {
                attributes.Add(
                        new RbacRule<V1CustomResourceDefinition>(
                            verbs: RbacVerb.Get | RbacVerb.List | RbacVerb.Watch,
                            scope: Resources.EntityScope.Cluster
                            ));
            }

            if (operatorSettings.hasMutatingWebhooks)
            {
                attributes.Add(
                        new RbacRule<V1MutatingWebhookConfiguration>(
                            verbs: RbacVerb.All,
                            scope: Resources.EntityScope.Cluster
                            ));
            }

            if (operatorSettings.hasValidatingWebhooks)
            {
                attributes.Add(
                        new RbacRule<V1ValidatingWebhookConfiguration>(
                            verbs: RbacVerb.All,
                            scope: Resources.EntityScope.Cluster
                            ));
            }

            if (operatorSettings.certManagerEnabled)
            {
                attributes.Add(
                    new RbacRule<V1Certificate>(
                        verbs: RbacVerb.All,
                        scope: Resources.EntityScope.Namespaced,
                        @namespace: operatorSettings.deployedNamespace
                        ));
                attributes.Add(
                    new RbacRule<V1Secret>(
                        verbs: RbacVerb.Watch,
                        scope: Resources.EntityScope.Namespaced,
                        @namespace: operatorSettings.deployedNamespace,
                        resourceNames: $"{operatorSettings.Name}-webhook-tls"
                        ));
            }

            var clusterRules = attributes.Where(attr => attr.Scope == Resources.EntityScope.Cluster)
                .GroupBy(attr => new
                {
                    ResourceNames = attr.ResourceNames?.Split(',').Distinct(),
                    Verbs = attr.Verbs
                })
                .Select(
                    group => (
                        Verbs: group.Key.Verbs,
                        ResourceNames: group.Key.ResourceNames,
                        EntityTypes: group.Select(attr => attr.GetKubernetesEntityAttribute()).ToList()))

                .Select(
                    group => new V1PolicyRule
                    {
                        ApiGroups = group.EntityTypes.Select(entity => entity.Group).Distinct().ToList(),
                        Resources = group.EntityTypes.Select(entity => entity.PluralName).Distinct().ToList(),
                        ResourceNames = group.ResourceNames?.Count() > 0 ? group.ResourceNames.ToList() : null,
                        Verbs = group.Verbs.ToStrings(),
                    });

            if (clusterRules.Any())
            {
                var clusterRole = new V1ClusterRole().Initialize();
                clusterRole.Metadata.Name = operatorSettings.Name;
                clusterRole.Rules = clusterRules.ToList();

                ClusterRoles.Add(clusterRole);

                var clusterRoleBinding = new V1ClusterRoleBinding().Initialize();
                clusterRoleBinding.Metadata.Name = operatorSettings.Name;
                clusterRoleBinding.RoleRef = new V1RoleRef(name: clusterRole.Metadata.Name, apiGroup: "rbac.authorization.k8s.io", kind: "ClusterRole");
                clusterRoleBinding.Subjects = new List<V1Subject>()
                {
                    new V1Subject(kind: "ServiceAccount", name: operatorSettings.Name, namespaceProperty: serviceAccount.Namespace())
                };

                ClusterRoleBindings.Add(clusterRoleBinding);
            }

            var namespaceRules = new Dictionary<string, List<V1PolicyRule>>();

            namespaceRules[operatorSettings.deployedNamespace] = attributes.Select(a => (IRbacRule)a).Where(attr =>
                attr.Scope == Resources.EntityScope.Namespaced)
                    .GroupBy(attr => new
                    {
                        ResourceNames = attr.ResourceNames?.Split(',').Distinct(),
                        Verbs = attr.Verbs
                    })
                    .Select(
                        group => (
                            Verbs: group.Key.Verbs,
                            ResourceNames: group.Key.ResourceNames,
                            EntityTypes: group.Select(attr => attr.GetKubernetesEntityAttribute()).ToList()))

                    .Select(
                        group => new V1PolicyRule
                        {
                            ApiGroups = group.EntityTypes.Select(entity => entity.Group).Distinct().ToList(),
                            Resources = group.EntityTypes.Select(entity => entity.PluralName).Distinct().ToList(),
                            ResourceNames = group.ResourceNames?.Count() > 0 ? group.ResourceNames.ToList() : null,
                            Verbs = group.Verbs.ToStrings(),
                        }).ToList();

            foreach (var reg in componentRegister.ResourceManagerRegistrations)
            {
                var resourceManager = (IResourceManager)serviceProvider.GetRequiredService(reg);
                var options = resourceManager.Options();

                var namespaces = options.RbacRules
                    .Where(r => r.Namespaces() != null)
                    .SelectMany(r => r.Namespaces()).Distinct().ToList();

                foreach (var @namespace in namespaces)
                {
                    namespaceRules[@namespace] = options.RbacRules
                        .Select(r => (IRbacRule)r)
                        .Where(attr =>
                            attr.Scope == Resources.EntityScope.Namespaced
                            && attr.Namespaces().Contains(@namespace))
                        .GroupBy(attr => new
                        {
                            ResourceNames = attr.ResourceNames?.Split(',').Distinct(),
                            Verbs = attr.Verbs,
                        })
                        .Select(
                            group => (
                                Verbs: group.Key.Verbs,
                                ResourceNames: group.Key.ResourceNames,
                                EntityTypes: group.Select(attr => attr.GetKubernetesEntityAttribute()).ToList()))

                        .Select(
                            group => new V1PolicyRule
                            {
                                ApiGroups = group.EntityTypes.Select(entity => entity.Group).Distinct().ToList(),
                                Resources = group.EntityTypes.Select(entity => entity.PluralName).Distinct().ToList(),
                                ResourceNames = group.ResourceNames?.Count() > 0 ? group.ResourceNames.ToList() : null,
                                Verbs = group.Verbs.ToStrings(),
                            }).ToList();
                }
            }

            if (namespaceRules.Keys.Any())
            {
                foreach (var @namespace in namespaceRules.Keys)
                {
                    var namespacedRole = new V1Role().Initialize();
                    namespacedRole.Metadata.Name = operatorSettings.Name;
                    namespacedRole.Metadata.NamespaceProperty = @namespace;
                    namespacedRole.Rules = namespaceRules[@namespace].ToList();

                    Roles.Add(namespacedRole);

                    var roleBinding = new V1RoleBinding().Initialize();
                    roleBinding.Metadata.Name = operatorSettings.Name;
                    roleBinding.Metadata.NamespaceProperty = @namespace;
                    roleBinding.RoleRef = new V1RoleRef(name: namespacedRole.Metadata.Name, apiGroup: "rbac.authorization.k8s.io", kind: "Role");
                    roleBinding.Subjects = new List<V1Subject>()
                    {
                        new V1Subject(kind: "ServiceAccount", name: operatorSettings.Name, namespaceProperty: serviceAccount.Namespace())
                    };

                    RoleBindings.Add(roleBinding);
                }
            }
        }
    }
}
