//-----------------------------------------------------------------------------
// FILE:	    CodeGenerator.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// $todo(jeff.lill):
//
// At somepoint in the future it would be nice to read the
// XML code documentation and include this in the generated
// source code as well.

namespace Neon.CodeGen
{
    /// <summary>
    /// Implements data model and service client code generation.
    /// </summary>
    public class CodeGenerator
    {
        private CodeGeneratorOutput                 output;
        private Dictionary<string, DataModel>       nameToDataModel    = new Dictionary<string, DataModel>();
        private Dictionary<string, ServiceModel>    nameToServiceModel = new Dictionary<string, ServiceModel>();
        private Dictionary<string, NamespaceInfo>   nameToNamespace    = new Dictionary<string, NamespaceInfo>();
        private StringWriter                        writer;
        private string                              targetGroup;
        private string                              targetNamespace;

        /// <summary>
        /// Constructs a code generator.
        /// </summary>
        /// <param name="settings">Optional settings.  Reasonable defaults will be used when this is <c>null</c>.</param>
        public CodeGenerator(CodeGeneratorSettings settings = null)
        {
            this.Settings = settings ?? new CodeGeneratorSettings();
        }

        /// <summary>
        /// Returns the code generation settings.
        /// </summary>
        public CodeGeneratorSettings Settings { get; private set; }

        /// <summary>
        /// Generates code from a set of source assemblies.
        /// </summary>
        /// <param name="assemblies">The source assemblies.</param>
        /// <returns>A <see cref="CodeGeneratorOutput"/> instance holding the results.</returns>
        public CodeGeneratorOutput Generate(IEnumerable<Assembly> assemblies)
        {
            Covenant.Requires<ArgumentNullException>(assemblies != null);
            Covenant.Requires<ArgumentException>(assemblies.Count() > 0, "At least one assembly must be passed.");

            output = new CodeGeneratorOutput();
            writer = new StringWriter();

            // Load and normalize service and data models from the source assemblies.

            foreach (var assembly in assemblies)
            {
                ScanAssembly(assembly);
            }

            FilterModels();

            // Verify that everything looks good.

            CheckForErrors();

            if (output.HasErrors)
            {
                return output;
            }

            // Perform the code generation.

            GenerateCode();

            return output;
        }

        /// <summary>
        /// <para>
        /// Scans an assembly for data and service models and loads information about these
        /// to <see cref="nameToDataModel"/> and <see cref="nameToServiceModel"/>.
        /// </para>
        /// <note>
        /// This method will honor any target filters specified by
        /// <see cref="CodeGeneratorSettings.TargetGroups"/>.
        /// </note>
        /// </summary>
        /// <param name="assembly">The source assembly.</param>
        private void ScanAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                return;
            }

            // Load and normalize the types.

            foreach (var type in assembly.GetTypes()
                .Where(t => t.IsPublic)
                .Where(t => t.IsInterface || t.IsEnum))
            {
                var serviceAttribute = type.GetCustomAttribute<ServiceAttribute>();

                if (serviceAttribute != null)
                {
                    LoadServiceModel(type);
                }
                else
                {
                    LoadDataModel(type);
                }
            }
        }

        /// <summary>
        /// Removes any data and/or service models that are not within any 
        /// of the targeted groups.
        /// </summary>
        private void FilterModels()
        {
            // Remove any data models that aren't in one of the target groups.

            var deletedDataModels = new List<string>();

            foreach (var item in nameToDataModel)
            {
                var inGroup = false;

                foreach (var group in Settings.TargetGroups)
                {
                    if (inGroup = item.Value.TargetGroups.Contains(group))
                    {
                        break;
                    }
                }

                if (!inGroup)
                {
                    deletedDataModels.Add(item.Key);
                }
            }

            foreach (var deletedDataModel in deletedDataModels)
            {
                nameToDataModel.Remove(deletedDataModel);
            }

            // Remove any service models aren't in one of the target groups.

            var deletedServiceModels = new List<string>();

            foreach (var item in nameToServiceModel)
            {
                var inGroup = false;

                foreach (var group in Settings.TargetGroups)
                {
                    if (inGroup = item.Value.TargetGroups.Contains(group))
                    {
                        break;
                    }
                }

                if (!inGroup)
                {
                    deletedServiceModels.Add(item.Key);
                }
            }

            foreach (var deletedServiceModel in deletedServiceModels)
            {
                nameToServiceModel.Remove(deletedServiceModel);
            }
        }

        /// <summary>
        /// Loads the required information for a service model type.
        /// </summary>
        /// <param name="type">The source type.</param>
        private void LoadServiceModel(Type type)
        {
            var serviceModel = new ServiceModel(type);

            nameToServiceModel[type.FullName] = serviceModel;

            foreach (var targetAttibute in type.GetCustomAttributes<TargetAttribute>())
            {
                if (!serviceModel.TargetGroups.Contains(targetAttibute.Group))
                {
                    serviceModel.TargetGroups.Add(targetAttibute.Group);
                }
            }

            var serviceAttributes = type.GetCustomAttribute<ServiceAttribute>();

            serviceModel.ClientTypeName = serviceAttributes.Name ?? type.Name;

            var clientGroupAttribute = type.GetCustomAttribute<ClientGroupAttribute>();

            if (clientGroupAttribute != null)
            {
                serviceModel.ClientGroup = clientGroupAttribute.Name;
            }

            var routeAttribute = type.GetCustomAttribute<RouteAttribute>();

            if (routeAttribute != null)
            {
                serviceModel.RouteTemplate = routeAttribute.Template;
            }

            // Walk the service methods to load their metadata.

            foreach (var methodInfo in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var serviceMethod = new ServiceMethod();

                routeAttribute = methodInfo.GetCustomAttribute<RouteAttribute>();

                if (routeAttribute != null)
                {
                    serviceMethod.RouteTemplate = routeAttribute.Template;
                }

                var httpAttribute = methodInfo.GetCustomAttribute<HttpAttribute>();

                if (httpAttribute != null)
                {
                    serviceMethod.Name       = routeAttribute.Name;
                    serviceMethod.HttpMethod = httpAttribute.HttpMethod;
                }

                if (string.IsNullOrEmpty(serviceMethod.Name))
                {
                    serviceMethod.Name = methodInfo.Name;
                }

                if (string.IsNullOrEmpty(serviceMethod.HttpMethod))
                {
                    if (methodInfo.Name.StartsWith("Delete", StringComparison.InvariantCultureIgnoreCase))
                    {
                        serviceMethod.HttpMethod = "DELETE";
                    }
                    else if (methodInfo.Name.StartsWith("Get", StringComparison.InvariantCultureIgnoreCase))
                    {
                        serviceMethod.HttpMethod = "GET";
                    }
                    else if (methodInfo.Name.StartsWith("Head", StringComparison.InvariantCultureIgnoreCase))
                    {
                        serviceMethod.HttpMethod = "HEAD";
                    }
                    else if (methodInfo.Name.StartsWith("Options", StringComparison.InvariantCultureIgnoreCase))
                    {
                        serviceMethod.HttpMethod = "OPTIONS";
                    }
                    else if (methodInfo.Name.StartsWith("Patch", StringComparison.InvariantCultureIgnoreCase))
                    {
                        serviceMethod.HttpMethod = "PATCH";
                    }
                    else if (methodInfo.Name.StartsWith("Post", StringComparison.InvariantCultureIgnoreCase))
                    {
                        serviceMethod.HttpMethod = "POST";
                    }
                    else if (methodInfo.Name.StartsWith("Put", StringComparison.InvariantCultureIgnoreCase))
                    {
                        serviceMethod.HttpMethod = "PUT";
                    }
                    else
                    {
                        // All other method names will default to: GET

                        serviceMethod.HttpMethod = "GET";
                    }
                }

                serviceModel.Methods.Add(serviceMethod);
            }
        }

        /// <summary>
        /// Loads the required information for a data model type.
        /// </summary>
        /// <param name="type">YThe source type.</param>
        private void LoadDataModel(Type type)
        {
            var dataModel = new DataModel(type);

            nameToDataModel[type.FullName] = dataModel;
            dataModel.BaseType             = type.BaseType;
            dataModel.IsEnum               = type.IsEnum;

            foreach (var targetAttibute in type.GetCustomAttributes<TargetAttribute>())
            {
                if (!dataModel.TargetGroups.Contains(targetAttibute.Group))
                {
                    dataModel.TargetGroups.Add(targetAttibute.Group);
                }
            }

            var dataModelAttribute = type.GetCustomAttribute<DataModelAttribute>();

            if (dataModelAttribute != null)
            {
                dataModel.TypeID = dataModelAttribute.TypeID ?? type.FullName;
            }

            if (string.IsNullOrEmpty(dataModel.TypeID))
            {
                dataModel.TypeID = type.FullName;
            }

            if (dataModel.IsEnum)
            {
                // Normalize the enum properties.

                dataModel.HasEnumFlags = type.GetCustomAttribute<FlagsAttribute>() != null;

                var enumBaseType = type.GetEnumUnderlyingType();

                if (enumBaseType == typeof(byte))
                {
                    dataModel.EnumBaseType = "byte";
                }
                else if (enumBaseType == typeof(sbyte))
                {
                    dataModel.EnumBaseType = "sbyte";
                }
                else if (enumBaseType == typeof(short))
                {
                    dataModel.EnumBaseType = "short";
                }
                else if (enumBaseType == typeof(ushort))
                {
                    dataModel.EnumBaseType = "ushort";
                }
                else if (enumBaseType == typeof(int))
                {
                    dataModel.EnumBaseType = "int";
                }
                else if (enumBaseType == typeof(uint))
                {
                    dataModel.EnumBaseType = "uint";
                }
                else if (enumBaseType == typeof(long))
                {
                    dataModel.EnumBaseType = "long";
                }
                else if (enumBaseType == typeof(ulong))
                {
                    dataModel.EnumBaseType = "ulong";
                }
                else 
                {
                    output.Errors.Add($"*** ERROR: [{type.FullName}]: Enumeration base type [{enumBaseType.FullName}] is not supported.");

                    dataModel.EnumBaseType = "int";
                }

                foreach (var member in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var enumMember = new EnumMember()
                    {
                        Name         = member.Name,
                        OrdinalValue = member.GetRawConstantValue().ToString()
                    };

                    var enumMemberAttribute = member.GetCustomAttribute<EnumMemberAttribute>();

                    if (enumMemberAttribute != null)
                    {
                        enumMember.SerializedName = enumMemberAttribute.Value;
                    }

                    if (string.IsNullOrEmpty(enumMember.SerializedName))
                    {
                        enumMember.SerializedName = member.Name;
                    }

                    dataModel.EnumMembers.Add(enumMember);
                }
            }
            else
            {
                // Normalize regular (non-enum) data model properties.

                foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var property = new DataProperty()
                    {
                        Name = member.Name,
                        Type = member.PropertyType
                    };

                    property.Ignore = member.GetCustomAttribute<JsonIgnoreAttribute>() != null;

                    var jsonPropertyAttribute = member.GetCustomAttribute<JsonPropertyAttribute>();

                    if (jsonPropertyAttribute != null)
                    {
                        property.SerializedName = jsonPropertyAttribute.PropertyName;
                        property.Order          = jsonPropertyAttribute.Order;
                    }
                    else
                    {
                        // Properties without a specific order should be rendered 
                        // after any properties with a specifc order.

                        property.Order = int.MaxValue;
                    }

                    if (string.IsNullOrEmpty(property.SerializedName))
                    {
                        property.SerializedName = member.Name;
                    }
                }
            }
        }

        /// <summary>
        /// Checks the loaded service and data models for problems.
        /// </summary>
        private void CheckForErrors()
        {
            // Ensure that all data model property types are either a primitive
            // .NET type or reference another loaded data model.  Also ensure
            // that all non-primitive types have a public default constructor.

            foreach (var dataModel in nameToDataModel.Values)
            {
                if (dataModel.SourceType.IsPrimitive)
                {
                    continue;
                }

                var constructors = dataModel.SourceType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                if (!constructors.Any(c => c.GetParameters().Length == 0))
                {
                    output.Errors.Add($"*** ERROR: [{dataModel.SourceType.FullName}]: This data model does not define a public (parameter-less) default constructor.");
                }

                foreach (var property in dataModel.Properties)
                {
                    if (!nameToDataModel.ContainsKey(property.Type.FullName))
                    {
                        output.Errors.Add($"*** ERROR: [{dataModel.SourceType.FullName}]: This data model references type [{property.Type.FullName}] which is not defined in a source assembly.");
                    }
                }
            }

            // Ensure that all service method parameter and result types are either
            // a primitive .NET type or reference another loaded data model.

            foreach (var serviceModel in nameToServiceModel.Values)
            {
                foreach (var method in serviceModel.Methods)
                {
                    var returnType = method.MethodInfo.ReturnType;

                    if (!returnType.IsPrimitive && !nameToDataModel.ContainsKey(returnType.FullName))
                    {
                        output.Errors.Add($"*** ERROR: [{serviceModel.SourceType.FullName}]: Service model [{method.MethodInfo.Name}] returns [{returnType.FullName}] which is not defined in a source assembly.");
                    }

                    foreach (var parameter in method.MethodInfo.GetParameters())
                    {
                        output.Errors.Add($"*** ERROR: [{serviceModel.SourceType.FullName}]: Service model [{method.MethodInfo.Name}] as argument [{parameter.Name}:{parameter.ParameterType.FullName}] whose type is not defined in a source assembly.");
                    }
                }
            }

            // $todo(jeff.lill):
            //
            // Ensure that routes and method signatures will be unique for the generated
            // clients, taking client groups into account.  The top priority is verify
            // the routes because any problems these won't be detected until runtime.
            // Any method signature conflicts will be detected when the the generated
            // code is compiled.
        }

        /// <summary>
        /// Generates code from the input models.
        /// </summary>
        private void GenerateCode()
        {
            // Write the source code file header.

            writer.WriteLine($"//-----------------------------------------------------------------------------");
            writer.WriteLine($"// This file was generated by the [Neon.CodeGen] library.  Any");
            writer.WriteLine($"// manual changes will be lost when the file is regenerated.");
            writer.WriteLine();
            writer.WriteLine($"#pragma warning disable 1591");
            writer.WriteLine();
            writer.WriteLine($"using System;");
            writer.WriteLine($"using System.Collections.Generic;");
            writer.WriteLine($"using System.Dynamic;");
            writer.WriteLine($"using System.IO;");
            writer.WriteLine();

            if (Settings.RoundTrip)
            {
                writer.WriteLine($"using Newtonsoft.Json;");
                writer.WriteLine($"using Newtonsoft.Json.Linq;");
                writer.WriteLine();
            }

            // Generate namespaces and the models contained within each.

            foreach (var namespaceInfo in nameToNamespace.Values
                .OrderBy(ns => ns.OutputNamespace.ToLowerInvariant()))
            {
                writer.WriteLine($"//-----------------------------------------------------------------------------");
                writer.WriteLine();
                writer.WriteLine($"namespace {namespaceInfo.OutputNamespace}");
                writer.WriteLine($"{{");

                // Generate the data models.

                var index = 0;

                foreach (var dataModel in namespaceInfo.DataModels
                    .OrderBy(dm => dm.SourceType.Name.ToLowerInvariant()))
                {
                    GenerateDataModel(dataModel, index++);
                }

                // Generate the service clients (if enabled).

                if (Settings.ServiceClients)
                {
                }

                writer.WriteLine($"}}");
            }

            // Set the generated source code for the code generator output.

            output.OutputSource = writer.ToString();
        }

        /// <summary>
        /// Generates source code for a data model.
        /// </summary>
        /// <param name="dataModel">The data model.</param>
        /// <param name="index">Zero based index of the model within the current namespace.</param>
        private void GenerateDataModel(DataModel dataModel, int index)
        {
            if (index > 0)
            {
                // Add a blank line between type definitions within the namespace.

                writer.WriteLine();
            }

            writer.WriteLine($"    /// <summary>");
            writer.WriteLine($"    /// Generated from: {dataModel.SourceType.FullName}");
            writer.WriteLine($"    /// </summary>");

            if (dataModel.IsEnum)
            {
                if (dataModel.HasEnumFlags)
                {
                    writer.WriteLine($"    [Flags]");
                }

                writer.WriteLine($"    public enum {dataModel.SourceType.Name} : {dataModel.EnumBaseType}");
                writer.WriteLine($"    {{");

                foreach (var member in dataModel.EnumMembers)
                {
                    writer.WriteLine($"        {member.Name} = {member.OrdinalValue},");
                }

                writer.WriteLine($"    }}");
            }
            else
            {
                writer.WriteLine($"    public partial class {dataModel.SourceType.Name}");
                writer.WriteLine($"    {{");

                if (Settings.RoundTrip)
                {
                    writer.WriteLine($"        public static {dataModel.SourceType.Name} FromJson(string jsonText)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (string.IsNullOrEmpty(jsonText))");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                throw new ArgumentNullException(nameof(jsonText))");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            this.__JObject = new JObject(jsonText);");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        public static {dataModel.SourceType.Name} FromJObject(JObject jObject)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (jObject == null)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                throw new ArgumentNullException(nameof(jsonText))");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            this.__JObject = jObject;");
                    writer.WriteLine($"        }}");

                    if (dataModel.BaseType == null)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        protected JObject __JObject {{ get; set; }}");
                    }
                }

                foreach (var property in dataModel.Properties)
                {
                    if (Settings.RoundTrip)
                    {
                        writer.WriteLine();
                    }

                    if (property.Ignore)
                    {
                        writer.WriteLine($"        [JsonIgnore]");
                    }
                    else
                    {
                        if (property.Order < int.MaxValue)
                        {
                            writer.WriteLine($"        [JsonProperty(Name = \"{property.SerializedName}\", Order = {property.Order})]");
                        }
                        else
                        {
                            writer.WriteLine($"        [JsonProperty(Name = \"{property.SerializedName}\")]");
                        }
                    }

                    var propertyTypeName = ResoveTypeReference(property.Type);

                    if (Settings.RoundTrip)
                    {
                        writer.WriteLine($"        public {propertyTypeName} {property.Name}");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            get {{ return __JObject[{property.SerializedName}].ToObject<{propertyTypeName}>(); }}");
                        writer.WriteLine($"            set {{ __JObject[{property.SerializedName}] = value; }}");
                        writer.WriteLine($"        }}");
                    }
                    else
                    {
                        writer.WriteLine($"        public {propertyTypeName} {property.Name} {{ get; set; }}");
                    }
                }

                writer.WriteLine($"    }}");
            }
        }

        /// <summary>
        /// Resolves the type passed into a string, taking namespaces,
        /// arrays, and generic collection references into account.
        /// </summary>
        /// <param name="type">The referenced type.</param>
        /// <returns>The type reference as a string or <c>null</c> if the type is now valid.</returns>
        private string ResoveTypeReference(Type type)
        {
            return ResoveTypeReference(type, out var error);
        }

        /// <summary>
        /// Resolves the type passed into a string, taking namespaces,
        /// arrays, and generic collection references into account.
        /// </summary>
        /// <param name="type">The referenced type.</param>
        /// <param name="errorMessage">Returns as the error message when there's a problem.</param>
        /// <returns>The type reference as a string or <c>null</c> if the type is now valid.</returns>
        private string ResoveTypeReference(Type type, out string errorMessage)
        {
            errorMessage = null;

            // We currently handle references to the following types:
            //
            //      * Primitive .NET types
            //      * Common .NET types: TimeSpan, DateTime, DateTimeOffset
            //      * Data models loaded from the assembly and included
            //        in one of the targeted groups
            //      * Single dimension arrays of the types above
            //      * Generic IList<T>, where T is of the above types
            //      * Generic IEnumerable<T>, where T is one of the above types
            //      * IDictionary<TKey, TValue> where the TKey and
            //        TValue is one of the above types
            //
            // NOTE:
            //
            //      * IList<T> and IEnumerable<T> types will be converted
            //        to List<T>.

            if (type.IsPrimitive)
            {
                return type.Name;
            }
            else if (type == typeof(TimeSpan) || type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return type.Name;
            }
            else if (type.IsArray)
            {
                if (type.GetArrayRank() > 1)
                {
                    errorMessage = "Only single dimensional arrays are supported";
                    return null;
                }

                var elementType = type.GetElementType();
                var elementRef  = ResoveTypeReference(elementType, out errorMessage);

                if (elementRef == null)
                {
                    return null;
                }

                return $"{elementRef}[]";
            }
            else if (type.IsGenericType)
            {
                if (type.FullName == "System.Collections.Generic.List" ||
                    type.FullName == "System.Collections.Generic.IList")
                {
                    var elementTypes = type.GetGenericArguments();
                    var elementRef   = ResoveTypeReference(elementTypes[0], out errorMessage);

                    if (elementRef == null)
                    {
                        return null;
                    }

                    return $"List<{elementRef}>";
                }
                else if (type.FullName == "System.Collections.Generic.Dictionary" ||
                         type.FullName == "System.Collections.Generic.IDictionary")
                {
                    var elementTypes = type.GetGenericArguments();
                    var keyRef = ResoveTypeReference(elementTypes[0], out errorMessage);

                    if (keyRef == null)
                    {
                        return null;
                    }

                    var valueRef = ResoveTypeReference(elementTypes[1], out errorMessage);

                    if (valueRef == null)
                    {
                        return null;
                    }

                    return $"Dictionary<{keyRef}, {valueRef}>";
                }
                else
                {
                    errorMessage = $"[{type}] is not a supported type.";
                    return null;
                }
            }
            else
            {
                errorMessage = $"[{type}] is not a supported type.";
                return null;
            }
        }
    }
}
