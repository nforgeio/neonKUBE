using Neon.Tasks;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.CommandLine.Invocation;
using Neon.Kube.Operator.Generators;
using Neon.Kube.Operator.Builder;
using System.Reflection;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Operator.Attributes;
using k8s;
using k8s.Models;
using System.Data;
using System.Xml.Linq;
using Neon.Common;
using YamlDotNet;
using System.IdentityModel.Tokens.Jwt;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Neon.BuildInfo;
using System.Text.RegularExpressions;

namespace Neon.Kube.Operator.Hosting.Commands.Generate
{
    internal class GenerateRbacCommand : GenerateCommandBase
    {
        ISerializer Serializer;
        private ComponentRegister componentRegister;
        public GenerateRbacCommand(ComponentRegister componentRegister) : base("rbac", "Generate RBAC yaml for the operator.")
        {
            this.componentRegister = componentRegister;
            Handler = CommandHandler.Create(() => HandleCommand());

            Serializer = new SerializerBuilder()
                        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
        }

        private int HandleCommand()
        {
            var output = new StringBuilder();

            var attributes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .SelectMany(
                    t => t.GetCustomAttributes()
                    .Where(a => a.GetType().IsGenericType)
                    .Where(a => a.GetType().GetGenericTypeDefinition().IsEquivalentTo(typeof(RbacAttribute<>))));

            var clusterRules = attributes.Where(attr => ((IRbacAttribute)attr).Scope == Resources.EntityScope.Cluster)
                .GroupBy(attr => ((IRbacAttribute)attr).Verbs)
                    .Select(
                        group => (
                            Verbs: group.Key,
                            EntityTypes: group.Select(attr => ((IRbacAttribute)attr).GetKubernetesEntityAttribute()).ToList()))

                    .Select(
                        group => new V1PolicyRule
                        {
                            ApiGroups = group.EntityTypes.Select(entity => entity.Group).Distinct().ToList(),
                            Resources = group.EntityTypes.Select(entity => entity.PluralName).Distinct().ToList(),
                            Verbs = group.Verbs.ToStrings(),
                        });

            var operatorName = Regex.Replace(Assembly.GetEntryAssembly().GetName().Name, @"([a-z])([A-Z])", "$1-$2").ToLower();

            if (clusterRules.Any())
            {
                var cr = new V1ClusterRole().Initialize();
                cr.Metadata.Name = operatorName;
                cr.Rules = clusterRules.ToList();

                output.AppendLine(Serializer.Serialize(cr));
            }

            foreach (var @namespace in attributes.Where(attr => ((IRbacAttribute)attr).Scope == Resources.EntityScope.Namespaced)
                .Select(attr => ((IRbacAttribute)attr).Namespace)
                .Distinct())
            {
                var namespaceRules = attributes.Where(attr =>
                    ((IRbacAttribute)attr).Scope == Resources.EntityScope.Namespaced
                    && ((IRbacAttribute)attr).Namespace == @namespace)
                        .GroupBy(attr => ((IRbacAttribute)attr).Verbs)
                        .Select(
                            group => (
                                Verbs: group.Key,
                                EntityTypes: group.Select(attr => ((IRbacAttribute)attr).GetKubernetesEntityAttribute()).ToList()))

                        .Select(
                            group => new V1PolicyRule
                            {
                                ApiGroups = group.EntityTypes.Select(entity => entity.Group).Distinct().ToList(),
                                Resources = group.EntityTypes.Select(entity => entity.PluralName).Distinct().ToList(),
                                Verbs = group.Verbs.ToStrings(),
                            });

                if (namespaceRules.Any())
                {
                    var nr = new V1Role().Initialize();
                    nr.Metadata.Name = operatorName;
                    nr.Metadata.NamespaceProperty = @namespace;
                    nr.Rules = namespaceRules.ToList();

                    output.AppendLine(Serializer.Serialize(nr));
                }
            }

            return HandleCommand(output.ToString());
        }
    }
}
