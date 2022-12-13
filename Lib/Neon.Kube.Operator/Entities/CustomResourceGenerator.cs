//-----------------------------------------------------------------------------
// FILE:	    CustomResourceGenerator.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Rest.Serialization;

using Neon.Common;
using Neon.Kube.Resources;
using Neon.Tasks;

using k8s;
using k8s.Models;
using k8s.Versioning;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;
using System.Threading;
using System.IO.Abstractions;
using System.IO;
using Neon.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using System.Linq.Expressions;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// A tool for generating Kubernetes Custom Resources.
    /// </summary>
    public class CustomResourceGenerator
    {
        private readonly JsonSchemaGeneratorSettings jsonSchemaGeneratorSettings;
        private readonly JsonSerializerSettings serializerSettings;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CustomResourceGenerator(
            int maxDepth = 128,
            IEnumerable<JsonConverter> converters = null)
        {
            serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter>
                {
                    new Iso8601TimeSpanConverter(),
                    new StringEnumConverter()
                },
                MaxDepth = maxDepth
            };

            if (converters != null)
            {
                foreach (var converter in converters)
                {
                    serializerSettings.Converters.Add(converter);
                }
            }

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
                SerializerSettings = serializerSettings
            };
        }

        /// <summary>
        /// Generates a <see cref="V1CustomResourceDefinition"/> for a Kubernetes custom resource entity.
        /// </summary>
        /// <param name="resourceType"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<V1CustomResourceDefinition> GenerateCustomResourceDefinitionAsync(
            Type resourceType,
            CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            var scope           = GetScope(resourceType) ?? EntityScope.Namespaced;
            var entity          = resourceType.GetTypeInfo().GetCustomAttribute<KubernetesEntityAttribute>();
            var schema          = GenerateJsonSchema(resourceType);
            var pluralNameGroup = string.IsNullOrEmpty(entity.Group) ? entity.PluralName : $"{entity.PluralName}.{entity.Group}";

            var crd = new V1CustomResourceDefinition(
                            apiVersion: $"{V1CustomResourceDefinition.KubeGroup}/{V1CustomResourceDefinition.KubeApiVersion}",
                            kind: V1CustomResourceDefinition.KubeKind,
                            metadata: new V1ObjectMeta(
                                name: pluralNameGroup),
                            spec: new V1CustomResourceDefinitionSpec(
                                group: entity.Group,
                                names: new V1CustomResourceDefinitionNames(
                                    kind: entity.Kind,
                                    plural: entity.PluralName),
                                scope: scope.ToMemberString(),
                                versions: new List<V1CustomResourceDefinitionVersion>
                                {
                                    new V1CustomResourceDefinitionVersion(
                                        name: entity.ApiVersion,
                                        served: true,
                                        storage: true,
                                        schema: new V1CustomResourceValidation(schema)),
                                }));

            foreach (var version in crd.Spec.Versions)
            {
                if (version.Schema.OpenAPIV3Schema.Properties.TryGetValue("metadata", out var metadata))
                {
                    metadata.Description = null;
                }
            }

            return crd;
        }

        /// <summary>
        /// Writes a <see cref="V1CustomResourceDefinition"/> to a file.
        /// </summary>
        /// <param name="resourceDefinition">The Custom Resource Definition.</param>
        /// <param name="path">The file path.</param>
        /// <returns></returns>
        public async Task WriteToFile(V1CustomResourceDefinition resourceDefinition, string path)
        {
            using (var file = File.Create(path))
            {
                using (var writer = new StreamWriter(file))
                {
                    await writer.WriteLineAsync(KubernetesYaml.Serialize(resourceDefinition));
                }
            }
        }

        private EntityScope? GetScope(Type resourceType)
        {
            EntityScope scope;
            try
            {
                scope = resourceType.GetCustomAttribute<EntityScopeAttribute>().Scope;
            }
            catch
            {
                return null;
            }

            return scope;
        }

        private V1JSONSchemaProps GenerateJsonSchema(Type resourceType)
        {
            // start with JsonSchema
            var schema = JsonSchema.FromType(resourceType, jsonSchemaGeneratorSettings);

            // convert to JToken to make alterations

            var rootToken = JObject.Parse(schema.ToJson());
            var sRootToken = JsonObject.Create(JsonDocument.Parse(schema.ToJson()).RootElement);

            sRootToken = RewriteObject(sRootToken);
            sRootToken.Remove("$schema");
            sRootToken.Remove("definitions");

            var schemaProps = sRootToken.Deserialize<V1JSONSchemaProps>();

            return schemaProps;
        }

        private JsonObject RewriteObject(JsonObject sourceObject)
        {
            var targetObject = new JsonObject();

            var queue = new Queue<JsonObject>();
            queue.Enqueue(sourceObject);
            while (queue.Count != 0)
            {
                sourceObject = queue.Dequeue();
                foreach (var property in sourceObject.AsEnumerable())
                {
                    if (property.Key == "$ref")
                    {
                        // resolve the target of the "$ref"
                        var reference = sourceObject;
                        foreach (var part in property.Value.GetValue<string>().Split("/"))
                        {
                            if (part == "#")
                            {
                                reference = (JsonObject)reference.Root;
                            }
                            else if (reference.TryGetPropertyValue(part, out var propertyValue))
                            {
                                reference = (JsonObject)propertyValue;
                            }
                        }

                        // the referenced object should be merged into the current target
                        queue.Enqueue(reference);

                        // and $ref property is not added
                        continue;
                    }

                    if (property.Key == "additionalProperties")
                    {
                        try
                        {
                            var pValue = property.Value.GetValue<bool>();
                            
                            if (!pValue)
                            {
                                continue;
                            }
                        } catch { }
                    }

                    if (property.Key == "oneOf" &&
                        property.Value is JsonArray &&
                        property.Value.AsArray().Count == 1)
                    {
                        // a single oneOf array item should be merged into current object
                        queue.Enqueue(RewriteObject((JsonObject)property.Value.AsArray().Single()));

                        // and don't add the oneOf property
                        continue;
                    }

                    // all other properties are added after the value is rewritten recursively
                    if (!targetObject.TryGetPropertyValue(property.Key, out var value))
                    {
                        targetObject.Add(property.Key, RewriteToken(property.Value));
                    }
                }
            }

            return targetObject;
        }

        private JsonNode RewriteToken(JsonNode sourceToken)
        {
            if (sourceToken is JsonObject sourceObject)
            {
                return RewriteObject(sourceObject);
            }
            else if (sourceToken is JsonArray sourceArray)
            {
                return new JsonArray(sourceArray.Select(RewriteToken).ToArray<JsonNode>());
            }
            else
            {
                return JsonNode.Parse(sourceToken.ToJsonString());
            }
        }
    }
}
