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

namespace Neon.Kube.Operator.Hosting.Commands.Generate
{
    internal class GenerateRbacCommand : GenerateCommandBase
    {
        private ComponentRegister componentRegister;
        public GenerateRbacCommand(ComponentRegister componentRegister) : base("rbac", "Generate RBAC yaml for the operator.")
        {
            this.componentRegister = componentRegister;
            Handler = CommandHandler.Create(() => HandleCommand());
        }

        private int HandleCommand()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                     .SelectMany(s => s.GetTypes())
                     .Where(
                         t => t.GetCustomAttributes()
                         .Where(a => a.GetType().IsGenericType)
                         .Any(a => a.GetType().GetGenericTypeDefinition().IsEquivalentTo(typeof(RbacAttribute<>)))
                     );

            var clusterRole = new V1ClusterRole()
            {
                Rules = new List<V1PolicyRule>()
            };
            var roles = new Dictionary<string, V1Role>();

            foreach (var type in types)
            {
                var attributes = (IEnumerable<IRbacAttribute>)type.GetCustomAttributes()
                         .Where(a => a.GetType().IsGenericType)
                         .Where(a => a.GetType().GetGenericTypeDefinition().IsEquivalentTo(typeof(RbacAttribute<>)));

                var gs = ((IEnumerable<IRbacAttribute>)attributes)
                    .GroupBy(g => g.Verbs)
                    .Select(
                        group => (
                            Verbs: group.Key,
                            EntityTypes: group.Select(g => g.GetEntityType()).ToList()))

                    .Select(
                        group => new V1PolicyRule
                        {
                            ApiGroups = group.EntityTypes.Select(crd => ()crd.Group).Distinct().ToList(),
                            Resources = group.Crds.Select(crd => crd.Plural).Distinct().ToList(),
                            Verbs = group.Verbs.ConvertToStrings(),
                        });

                foreach (var g in groups)
                {
                    var a = attributes.Where(a => a.Verbs == g.Key);
                }

                foreach (IRbacAttribute attr in attributes)
                {
                    var entityType = attr.GetEntityType();

                    var typeMetadata = entityType.GetKubernetesTypeMetadata();

                    var rule = new V1PolicyRule();
                    rule.ApiGroups = new List<string>() { typeMetadata.Group };
                    rule.Verbs = attr.Verbs.ToStrings();
                    rule.Resources = new List<string>() { typeMetadata.PluralName };

                    if (attr.Scope == Resources.EntityScope.Cluster)
                    {
                        clusterRole.Rules.Add(rule);
                    }
                    else
                    {
                        if (roles.Keys.Contains(attr.Namespace))
                        {
                            roles[attr.Namespace].Rules.Add(rule);
                        }
                        else
                        {
                            roles.Add(attr.Namespace, new V1Role() { Rules= new List<V1PolicyRule>() { rule } });
                        }
                    }
                }

            }

            foreach (var group in clusterRole.Rules.GroupBy())

            return HandleCommand("Generating RBAC");
        }
    }
}
