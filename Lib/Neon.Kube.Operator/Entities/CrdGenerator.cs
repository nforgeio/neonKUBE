using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Rest.Serialization;

using Neon.Common;

using k8s;
using k8s.Models;
using k8s.Versioning;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;
using System.Threading;

namespace Neon.Kube.Operator
{
    internal class CrdGenerator
    {
        private const string Integer = "integer";
        private const string Number = "number";
        private const string String = "string";
        private const string Boolean = "boolean";
        private const string Object = "object";
        private const string Array = "array";

        private const string Int32 = "int32";
        private const string Int64 = "int64";
        private const string Float = "float";
        private const string Double = "double";
        private const string DateTime = "date-time";

        private readonly ComponentRegister componentRegister;
        private readonly JsonSchemaGeneratorSettings jsonSchemaGeneratorSettings;
        private readonly JsonSerializerSettings serializerSettings;


        public CrdGenerator(ComponentRegister componentRegister)
        {
            this.componentRegister = componentRegister;

            jsonSchemaGeneratorSettings = new JsonSchemaGeneratorSettings()
            {
                SchemaType = SchemaType.OpenApi3,
                TypeMappers =
            {
                new ObjectTypeMapper(
                    typeof(V1ObjectMeta),
                    new JsonSchema
                    {
                        Type = JsonObjectType.Object,
                    }),
            },
            };

            serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                ContractResolver = new ReadOnlyJsonContractResolver(),
                Converters = new List<JsonConverter>
            {
                new Iso8601TimeSpanConverter(),
            },
            };
        }

        public async Task<V1CustomResourceDefinition> GenerateCustomResourceDefinitionAsync(Type resourceType, CancellationToken cancellationToken = default)
        {
            KubernetesEntityAttribute kubernetesEntityAttribute = resourceType.GetCustomAttribute<KubernetesEntityAttribute>();
            var scope = resourceType.GetCustomAttribute<EntityScopeAttribute>().Scope.ToMemberString();
            var names = GroupApiVersionKind.From(resourceType);
            var schema = GenerateJsonSchema(resourceType);

            var crd = new V1CustomResourceDefinition(
                            apiVersion: $"{V1CustomResourceDefinition.KubeGroup}/{V1CustomResourceDefinition.KubeApiVersion}",
                            kind: V1CustomResourceDefinition.KubeKind,
                            metadata: new V1ObjectMeta(
                                name: names.PluralNameGroup),
                            spec: new V1CustomResourceDefinitionSpec(
                                group: names.Group,
                                names: new V1CustomResourceDefinitionNames(
                                    kind: names.Kind,
                                    plural: names.PluralName),
                                scope: scope,
                                versions: new List<V1CustomResourceDefinitionVersion>
                                {
                                    new V1CustomResourceDefinitionVersion(
                                        name: names.ApiVersion,
                                        served: true,
                                        storage: true,
                                        schema: new V1CustomResourceValidation(schema)),
                                }));


            return crd;
        }

        private V1JSONSchemaProps GenerateJsonSchema(Type resourceType)
        {
            // start with JsonSchema
            var schema = JsonSchema.FromType(resourceType, jsonSchemaGeneratorSettings);

            // convert to JToken to make alterations
            var rootToken = JObject.Parse(schema.ToJson());
            rootToken = RewriteObject(rootToken);
            rootToken.Remove("$schema");
            rootToken.Remove("definitions");

            // convert to k8s.Models.V1JSONSchemaProps to return
            using var reader = new JTokenReader(rootToken);
            return JsonSerializer
                .Create(serializerSettings)
                .Deserialize<V1JSONSchemaProps>(reader);
        }

        private JObject RewriteObject(JObject sourceObject)
        {
            var targetObject = new JObject();

            var queue = new Queue<JObject>();
            queue.Enqueue(sourceObject);
            while (queue.Count != 0)
            {
                sourceObject = queue.Dequeue();
                foreach (var property in sourceObject.Properties())
                {
                    if (property.Name == "$ref")
                    {
                        // resolve the target of the "$ref"
                        var reference = sourceObject;
                        foreach (var part in property.Value.Value<string>().Split("/"))
                        {
                            if (part == "#")
                            {
                                reference = (JObject)reference.Root;
                            }
                            else
                            {
                                reference = (JObject)reference[part];
                            }
                        }

                        // the referenced object should be merged into the current target
                        queue.Enqueue(reference);

                        // and $ref property is not added
                        continue;
                    }

                    if (property.Name == "additionalProperties" &&
                        property.Value.Type == JTokenType.Boolean &&
                        property.Value.Value<bool>() == false)
                    {
                        // don't add this property when it has a default value
                        continue;
                    }

                    if (property.Name == "oneOf" &&
                        property.Value.Type == JTokenType.Array &&
                        property.Value.Children().Count() == 1)
                    {
                        // a single oneOf array item should be merged into current object
                        queue.Enqueue(RewriteObject(property.Value.Children().Cast<JObject>().Single()));

                        // and don't add the oneOf property
                        continue;
                    }

                    // all other properties are added after the value is rewritten recursively
                    if (!targetObject.ContainsKey(property.Name))
                    {
                        targetObject.Add(property.Name, RewriteToken(property.Value));
                    }
                }
            }

            return targetObject;
        }

        private JToken RewriteToken(JToken sourceToken)
        {
            if (sourceToken is JObject sourceObject)
            {
                return RewriteObject(sourceObject);
            }
            else if (sourceToken is JArray sourceArray)
            {
                return new JArray(sourceArray.Select(RewriteToken));
            }
            else
            {
                return sourceToken;
            }
        }
    }
}
