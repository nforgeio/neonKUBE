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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Data;

// $todo(jeff.lill):
//
// At somepoint in the future it would be nice to read any
// XML code documentation and include this in the generated
// source code as well.

namespace Neon.CodeGen
{
    /// <summary>
    /// Handles data model and service client code generation.
    /// </summary>
    public class CodeGenerator
    {
        //---------------------------------------------------------------------
        // Static members

        private static MetadataReference    cachedNetStandard;
        private static Regex                routeConstraintRegex = new Regex(@"\{[^:\}]*:[^:\}]*\}");   // Matches route template parameters with constraints (like: "{param:int}").
        private static Regex                routeParameterRegex  = new Regex(@"\{([^\}]*)\}");          // Matches route template parameters (like: "{param}") capturing just the parameter name.

        /// <summary>
        /// Compiles C# source code into an assembly.
        /// </summary>
        /// <param name="source">The C# source code.</param>
        /// <param name="assemblyName">The generated assembly name.</param>
        /// <param name="referenceHandler">Called to manage metadata/assembly references (see remarks).</param>
        /// <param name="options">Optional compilation options.  This defaults to building a release assembly.</param>
        /// <returns>The compiled assembly as a <see cref="MemoryStream"/>.</returns>
        /// <exception cref="CompilerErrorException">Thrown for compiler errors.</exception>
        /// <remarks>
        /// <para>
        /// By default, this method will compile the assembly with references to 
        /// .NET Standard 2.0.
        /// </para>
        /// <para>
        /// You may customize these by passing a <paramref name="referenceHandler"/>
        /// action.  This is passed the list of <see cref="MetadataReference"/> instances.
        /// You can add or remove references as required.  The easiest way to add
        /// a reference is to use type reference like:
        /// </para>
        /// <code>
        /// using Microsoft.CodeAnalysis;
        /// 
        /// ...
        /// 
        /// var source   = "public class Foo {}";
        /// var assembly = CodeGenerator.Compile(source, "my-assembly",
        ///     references =>
        ///     {
        ///         references.Add(typeof(MyClass));    // Adds the assembly containing MyClass.
        ///     });
        /// </code>
        /// </remarks>
        public static MemoryStream Compile(
            string                          source, 
            string                          assemblyName, 
            Action<MetadataReferences>      referenceHandler = null,
            CSharpCompilationOptions        options          = null)
        {
            Covenant.Requires<ArgumentNullException>(source != null);

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new MetadataReferences();

            // Allow the caller to add references.

            referenceHandler?.Invoke(references);

            // Add the [Neon.Common] assembly.

            references.Add(typeof(IGeneratedEntity));

            // NOTE: 
            // 
            // We need add all of the NetStandard reference assemblies so
            // compilation will actually work.
            // 
            // We've set [PreserveCompilationContext=true] in [Neon.CodeGen.csproj]
            // so that the reference assemblies will be written to places like:
            //
            //      bin/Debug/netstandard2.0/refs/*
            //
            // This is where we obtained the these assemblies and added them
            // all as resources within the [Netstandard] project folder.
            //
            // We'll need to replace all of these when/if we upgrade the 
            // library to a new version of NetStandard.

            if (cachedNetStandard == null)
            {
                var thisAssembly = Assembly.GetExecutingAssembly();

                using (var resourceStream = thisAssembly.GetManifestResourceStream("Neon.CodeGen.Netstandard.netstandard.dll"))
                {
                    cachedNetStandard = MetadataReference.CreateFromStream(resourceStream);
                }
            }

            references.Add(cachedNetStandard);

            if (options == null)
            {
                options = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release);
            }

            var compilation = CSharpCompilation.Create(assemblyName, new[] { syntaxTree }, references, options);

            var dllStream = new MemoryStream();

            using (var pdbStream = new MemoryStream())
            {
                var emitted = compilation.Emit(dllStream, pdbStream);

                if (!emitted.Success)
                {
                    throw new CompilerErrorException(emitted.Diagnostics);
                }
            }

            dllStream.Position = 0;

            return dllStream;
        }

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, DataModel>       nameToDataModel    = new Dictionary<string, DataModel>();
        private Dictionary<string, ServiceModel>    nameToServiceModel = new Dictionary<string, ServiceModel>();
        private bool                                firstItemGenerated = true;
        private StringWriter                        writer;

        /// <summary>
        /// Constructs a code generator.
        /// </summary>
        /// <param name="settings">Optional settings.  Reasonable defaults will be used when this is <c>null</c>.</param>
        public CodeGenerator(CodeGeneratorSettings settings = null)
        {
            this.Settings = settings ?? new CodeGeneratorSettings();
            this.Output   = new CodeGeneratorOutput();

            if (string.IsNullOrEmpty(settings.SourceNamespace))
            {
                settings.SourceNamespace = null;
            }
            else
            {
                if (!settings.SourceNamespace.EndsWith("."))
                {
                    settings.SourceNamespace += ".";
                }
            }

            Settings.TargetNamespace = Settings.TargetNamespace ?? "Neon.CodeGen.Output";
        }

        /// <summary>
        /// Returns the code generation settings.
        /// </summary>
        public CodeGeneratorSettings Settings { get; private set; }

        /// <summary>
        /// Returns the code generator output instance.
        /// </summary>
        public CodeGeneratorOutput Output { get; private set; }

        /// <summary>
        /// Generates code from a set of source assemblies.
        /// </summary>
        /// <param name="assemblies">The source assemblies.</param>
        /// <returns>A <see cref="CodeGeneratorOutput"/> instance holding the results.</returns>
        public CodeGeneratorOutput Generate(params Assembly[] assemblies)
        {
            Covenant.Requires<ArgumentNullException>(assemblies != null);
            Covenant.Requires<ArgumentException>(assemblies.Length > 0, "At least one assembly must be passed.");

            writer = new StringWriter();

            // Load and normalize service and data models from the source assemblies.

            foreach (var assembly in assemblies)
            {
                ScanAssembly(assembly);
            }

            FilterModels();

            // Verify that everything looks good.

            CheckForErrors();

            if (Output.HasErrors)
            {
                return Output;
            }

            // Perform the code generation.

            GenerateCode();

            return Output;
        }

        /// <summary>
        /// <para>
        /// Scans an assembly for data and service models and loads information about these
        /// to <see cref="nameToDataModel"/> and <see cref="nameToServiceModel"/>.
        /// </para>
        /// <note>
        /// This method will honor any target filters specified by
        /// <see cref="CodeGeneratorSettings.Targets"/>.
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
                if (Settings.SourceNamespace != null && !type.FullName.StartsWith(Settings.SourceNamespace))
                {
                    // Ignore any types that aren't in specified source namespace.

                    continue;
                }

                if (type.GetCustomAttribute<NoCodeGenAttribute>() != null)
                {
                    // Ignore any types tagged with [NoCodeGen].

                    continue;
                }

                var serviceAttribute = type.GetCustomAttribute<ServiceModelAttribute>();

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
            if (Settings.Targets.Count == 0)
            {
                // Treat an empty list as enabling all targets.

                return;
            }

            // Remove any data models that aren't in one of the targets.

            var deletedDataModels = new List<string>();

            foreach (var item in nameToDataModel)
            {
                var targeted = false;

                foreach (var target in Settings.Targets)
                {
                    if (targeted = item.Value.Targets.Contains(target))
                    {
                        break;
                    }
                }

                if (!targeted)
                {
                    deletedDataModels.Add(item.Key);
                }
            }

            foreach (var deletedDataModel in deletedDataModels)
            {
                nameToDataModel.Remove(deletedDataModel);
            }

            // Remove any service models aren't in one of the targets.

            var deletedServiceModels = new List<string>();

            foreach (var item in nameToServiceModel)
            {
                var targeted = false;

                foreach (var target in Settings.Targets)
                {
                    if (targeted = item.Value.Targets.Contains(target))
                    {
                        break;
                    }
                }

                if (!targeted)
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
        /// <param name="serviceModelType">The source type.</param>
        private void LoadServiceModel(Type serviceModelType)
        {
            var serviceModel = new ServiceModel(serviceModelType, this);

            nameToServiceModel[serviceModelType.FullName] = serviceModel;

            foreach (var targetAttibute in serviceModelType.GetCustomAttributes<TargetAttribute>())
            {
                if (!serviceModel.Targets.Contains(targetAttibute.Name))
                {
                    serviceModel.Targets.Add(targetAttibute.Name);
                }
            }

            // Handle any [Route] or [RoutePrefix] tags.

            var serviceRouteAttribute = serviceModelType.GetCustomAttribute<RouteAttribute>();

            if (serviceRouteAttribute != null)
            {
                Output.Error($"[{serviceModelType.FullName}]: This data model defines method is tagged with the [Route] attribute.  This is not currently supported.");
            }

            var serviceRoutePrefixAttribute = serviceModelType.GetCustomAttribute<RoutePrefixAttribute>();

            if (serviceRoutePrefixAttribute != null && !string.IsNullOrEmpty(serviceRoutePrefixAttribute.Prefix))
            {
                serviceModel.RouteTemplate = serviceRoutePrefixAttribute.Prefix;
            }

            // Walk the service methods to load their metadata.

            foreach (var methodInfo in serviceModelType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var serviceMethod = new ServiceMethod(serviceModel)
                {
                    MethodInfo = methodInfo,
                    IsVoid     = ResolveTypeReference(methodInfo.ReturnType, isResultType: true) == "void"
                };

                var routeAttribute = methodInfo.GetCustomAttribute<RouteAttribute>();

                if (routeAttribute != null)
                {
                    // Verify that the template doesn't specify any route constraints.

                    if (!string.IsNullOrEmpty(routeAttribute.Template) && routeConstraintRegex.IsMatch(routeAttribute.Template))
                    {
                        Output.Error($"[{serviceModelType.FullName}]: This data model defines method [{serviceMethod.Name}] that defines a route template with a constraint.  Constraints are not currently supported.");
                    }

                    serviceMethod.RouteTemplate = ConcatRoutes(serviceModel.RouteTemplate, routeAttribute.Template);
                }
                else
                {
                    serviceMethod.RouteTemplate = ConcatRoutes(serviceModel.RouteTemplate, methodInfo.Name);
                }

                var httpAttribute = methodInfo.GetCustomAttribute<HttpAttribute>();

                if (httpAttribute != null)
                {
                    serviceMethod.Name       = httpAttribute.Name;
                    serviceMethod.HttpMethod = httpAttribute.HttpMethod.ToUpperInvariant();
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

                switch (serviceMethod.HttpMethod)
                {
                    case "DELETE":
                    case "GET":
                    case "HEAD":
                    case "OPTIONS":
                    case "PATCH":
                    case "POST":
                    case "PUT":

                        // All of these HTTP methods are currently supported.

                        break;

                    default:

                        // These HTTP methods are not supported.

                        Output.Error($"[{serviceModelType.FullName}]: This data model defines method [{serviceMethod.Name}] that uses the unsupported HTTP [{serviceMethod.HttpMethod}].");
                        break;
                }

                // Read and normalize the method parameters.

                foreach (var parameterInfo in serviceMethod.MethodInfo.GetParameters())
                {
                    var methodParameter = new MethodParameter(parameterInfo);

                    // Process and normalize the parameter passing attributes.

                    var fromAttributeCount = 0;
                    var fromBodyAttribute  = parameterInfo.GetCustomAttribute<FromBodyAttribute>();

                    if (fromBodyAttribute != null)
                    {
                        fromAttributeCount++;
                        methodParameter.Pass = Pass.AsBody;
                    }

                    var fromHeaderAttribute = parameterInfo.GetCustomAttribute<FromHeaderAttribute>();

                    if (fromHeaderAttribute != null)
                    {
                        fromAttributeCount++;

                        methodParameter.Pass           = Pass.AsHeader;
                        methodParameter.SerializedName = fromHeaderAttribute.Name ?? parameterInfo.Name;
                    }

                    var fromQueryAttribute = parameterInfo.GetCustomAttribute<FromQueryAttribute>();

                    if (fromQueryAttribute != null)
                    {
                        fromAttributeCount++;

                        methodParameter.Pass           = Pass.InQuery;
                        methodParameter.SerializedName = fromQueryAttribute.Name ?? parameterInfo.Name;
                    }

                    var fromRouteAttribute = parameterInfo.GetCustomAttribute<FromRouteAttribute>();

                    if (fromRouteAttribute != null)
                    {
                        fromAttributeCount++;

                        methodParameter.Pass           = Pass.InRoute;
                        methodParameter.SerializedName = fromRouteAttribute.Name ?? parameterInfo.Name;
                    }

                    if (fromAttributeCount == 0)
                    {
                        // Default to [FromQuery] using the parameter name.

                        methodParameter.Pass           = Pass.InQuery;
                        methodParameter.SerializedName = parameterInfo.Name;
                    }
                    else if (fromAttributeCount > 1)
                    {
                        Output.Error($"Service method [{serviceMethod.ServiceModel.SourceType.Name}.{serviceMethod.Name}(...)] defines parameter [{parameterInfo.Name}] with multiple [FromXXX] attributes.  A maximum of one is allowed.");
                    }

                    // Verify that the parameter type is valid.

                    if (!IsValidMethodType(methodParameter.ParameterInfo.ParameterType, methodParameter.Pass))
                    {
                        Output.Error($"Service method [{serviceMethod.ServiceModel.SourceType.Name}.{serviceMethod.Name}(...)] defines parameter [{parameterInfo.Name}] with unsupported type [{parameterInfo.ParameterType.Name}].  Consider tagging the parameter with [FromBody].");
                    }

                    serviceMethod.Parameters.Add(methodParameter);
                }

                var asBodyParameterCount = serviceMethod.Parameters.Count(p => p.Pass == Pass.AsBody);

                if (asBodyParameterCount > 1)
                {
                    Output.Error($"Service method [{serviceMethod.ServiceModel.SourceType.Name}.{serviceMethod.Name}(...)] defines more than one parameter tagged with [FromBody].");
                }

                if (serviceMethod.HttpMethod == "GET" && asBodyParameterCount > 0)
                {
                    Output.Error($"Service method [{serviceMethod.ServiceModel.SourceType.Name}.{serviceMethod.Name}(...)] a parameter tagged with [FromBody].  This is not alowed for methods using the HTTP GET method.");
                }

                serviceModel.Methods.Add(serviceMethod);
            }
        }

        /// <summary>
        /// Loads the required information for a data model type.
        /// </summary>
        /// <param name="dataType">The source data model type.</param>
        private void LoadDataModel(Type dataType)
        {
            if (dataType.IsGenericTypeDefinition)
            {
                Output.Error($"Data model [{dataType.FullName}] is not currently supported because it is a generic type.");
                return;
            }

            var dataModel = new DataModel(dataType, this);

            nameToDataModel[dataType.FullName] = dataModel;
            dataModel.IsEnum                   = dataType.IsEnum;

            foreach (var targetAttibute in dataType.GetCustomAttributes<TargetAttribute>())
            {
                if (!dataModel.Targets.Contains(targetAttibute.Name))
                {
                    dataModel.Targets.Add(targetAttibute.Name);
                }
            }

            var dataModelAttribute = dataType.GetCustomAttribute<DataModelAttribute>();

            if (dataModelAttribute != null)
            {
                dataModel.EntityType = dataModelAttribute.EntityType ?? dataType.FullName;
            }

            if (string.IsNullOrEmpty(dataModel.EntityType))
            {
                dataModel.EntityType = dataType.FullName;
            }

            if (dataModel.IsEnum)
            {
                // Normalize the enum properties.

                dataModel.HasEnumFlags = dataType.GetCustomAttribute<FlagsAttribute>() != null;

                var enumBaseType = dataType.GetEnumUnderlyingType();

                if (enumBaseType == typeof(byte))
                {
                    dataModel.BaseTypeName = "byte";
                }
                else if (enumBaseType == typeof(sbyte))
                {
                    dataModel.BaseTypeName = "sbyte";
                }
                else if (enumBaseType == typeof(short))
                {
                    dataModel.BaseTypeName = "short";
                }
                else if (enumBaseType == typeof(ushort))
                {
                    dataModel.BaseTypeName = "ushort";
                }
                else if (enumBaseType == typeof(int))
                {
                    dataModel.BaseTypeName = "int";
                }
                else if (enumBaseType == typeof(uint))
                {
                    dataModel.BaseTypeName = "uint";
                }
                else if (enumBaseType == typeof(long))
                {
                    dataModel.BaseTypeName = "long";
                }
                else if (enumBaseType == typeof(ulong))
                {
                    dataModel.BaseTypeName = "ulong";
                }
                else 
                {
                    Output.Error($"[{dataType.FullName}]: Enumeration base type [{enumBaseType.FullName}] is not supported.");

                    dataModel.BaseTypeName = "int";
                }

                foreach (var member in dataType.GetFields(BindingFlags.Public | BindingFlags.Static))
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
                dataModel.Persisted = dataType.GetCustomAttribute<PersistedAttribute>();

                // A data model interface is allowed to implement another 
                // data model interface to specify a base class.  Note that
                // only one of these references is allowed and it may only
                // be a reference to another data model (not an arbtrary 
                // type).

                var baseInterface = (Type)null;

                foreach (var implementedInterface in dataType.GetInterfaces())
                {
                    if (!nameToDataModel.ContainsKey(implementedInterface.FullName))
                    {
                        Output.Error($"[{dataModel.SourceType.FullName}]: This data model inherits [{implementedInterface.FullName}] which is not defined as a data model.");
                    }

                    if (baseInterface != null)
                    {
                        Output.Error($"[{dataModel.SourceType.FullName}]: This data model inherits from multiple base types.  A maximum of one is allowed.");
                    }

                    baseInterface = implementedInterface;
                }

                dataModel.BaseTypeName = baseInterface?.FullName;

                if (baseInterface != null)
                {
                    dataModel.BaseModel = nameToDataModel[baseInterface.FullName];
                }

                // Normalize regular (non-enum) data model properties.

                foreach (var member in dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Ignore properties that don't have both a getter and a setter.

                    if (member.GetAccessors().Length != 2)
                    {
                        continue;
                    }

                    var property = new DataProperty(Output)
                    {
                        Name = member.Name,
                        Type = member.PropertyType
                    };

                    property.Ignore       = member.GetCustomAttribute<JsonIgnoreAttribute>() != null;
                    property.IsHashSource = member.GetCustomAttribute<HashSourceAttribute>() != null;

                    var jsonPropertyAttribute = member.GetCustomAttribute<JsonPropertyAttribute>();

                    if (jsonPropertyAttribute != null)
                    {
                        property.SerializedName       = jsonPropertyAttribute.PropertyName;
                        property.Order                = jsonPropertyAttribute.Order;
                        property.Required             = jsonPropertyAttribute.Required;
                        property.DefaultValueHandling = jsonPropertyAttribute.DefaultValueHandling;
                    }
                    else
                    {
                        // Properties without a specific order should be rendered 
                        // after any properties with a specifc order.

                        property.Order = int.MaxValue;
                    }

                    var defaultValueAttribute = member.GetCustomAttribute<DefaultValueAttribute>();

                    if (defaultValueAttribute != null)
                    {
                        property.DefaultValue = defaultValueAttribute.Value;
                    }

                    if (string.IsNullOrEmpty(property.SerializedName))
                    {
                        property.SerializedName = member.Name;
                    }

                    dataModel.Properties.Add(property);
                }
            }
        }

        /// <summary>
        /// Checks the loaded service and data models for problems.
        /// </summary>
        private void CheckForErrors()
        {
            // Ensure that all data model property types are either a primitive
            // .NET type, a type implemented within [mscorlib] or reference another
            // loaded data model.  Also ensure that all non-primitive types have a 
            // public default constructor.

            foreach (var dataModel in nameToDataModel.Values)
            {
                if (dataModel.SourceType.IsPrimitive)
                {
                    continue;
                }

                foreach (var property in dataModel.Properties)
                {
                    var propertyType = property.Type;

                    if (IsSafeType(propertyType))
                    {
                        continue;
                    }

                    if (!nameToDataModel.ContainsKey(propertyType.FullName))
                    {
                        Output.Error($"[{dataModel.SourceType.FullName}]: This data model references type [{propertyType.FullName}] which is not defined as a data model.");
                    }
                }
            }

            // Ensure that all service method parameter and result types are either
            // a primitive .NET type, a type implemented within [mscorlib] or 
            // reference a loaded data model.

            foreach (var serviceModel in nameToServiceModel.Values)
            {
                foreach (var method in serviceModel.Methods)
                {
                    var returnType = method.MethodInfo.ReturnType;

                    if (ResolveTypeReference(returnType, isResultType: true) == null)
                    {
                        Output.Error($"[{serviceModel.SourceType.FullName}]: Service model [{method.MethodInfo.Name}] returns [{returnType.FullName}] which is not defined as a data model.");
                    }

                    foreach (var parameter in method.Parameters)
                    {
                        if (!IsValidMethodType(parameter.ParameterInfo.ParameterType, parameter.Pass))
                        {
                            Output.Error($"[{serviceModel.SourceType.FullName}]: Service model [{method.MethodInfo.Name}] has argument [{parameter.Name}:{parameter.ParameterInfo.ParameterType.FullName}] whose type is not a defined data model.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates code from the input models.
        /// </summary>
        private void GenerateCode()
        {
            // Write the source code file header.

            writer.WriteLine($"//-----------------------------------------------------------------------------");
            writer.WriteLine($"// This file was generated by the [Neon.CodeGen] library.  Any manual changes");
            writer.WriteLine($"// will be lost when the file is regenerated.");
            writer.WriteLine();
            writer.WriteLine($"#pragma warning disable 0108     // Disable property overrides without new warnings");
            writer.WriteLine($"#pragma warning disable 0168     // Disable declared but never used warnings");
            writer.WriteLine($"#pragma warning disable 1591     // Disable missing comment warnings");
            writer.WriteLine();
            writer.WriteLine($"using System;");
            writer.WriteLine($"using System.Collections.Generic;");
            writer.WriteLine($"using System.ComponentModel;");
            writer.WriteLine($"using System.Dynamic;");
            writer.WriteLine($"using System.IO;");
            writer.WriteLine($"using System.Linq;");
            writer.WriteLine($"using System.Linq.Expressions;");
            writer.WriteLine($"using System.Net;");
            writer.WriteLine($"using System.Net.Http;");
            writer.WriteLine($"using System.Net.Http.Headers;");
            writer.WriteLine($"using System.Reflection;");
            writer.WriteLine($"using System.Runtime.Serialization;");
            writer.WriteLine($"using System.Text;");
            writer.WriteLine($"using System.Threading;");
            writer.WriteLine($"using System.Threading.Tasks;");
            writer.WriteLine();
            writer.WriteLine($"using Neon.Collections;");
            writer.WriteLine($"using Neon.Common;");
            writer.WriteLine($"using Neon.Data;");
            writer.WriteLine($"using Neon.Diagnostics;");
            writer.WriteLine($"using Neon.Net;");
            writer.WriteLine($"using Neon.Retry;");
            writer.WriteLine();

            if (Settings.RoundTrip)
            {
                writer.WriteLine($"using Newtonsoft.Json;");
                writer.WriteLine($"using Newtonsoft.Json.Converters;");
                writer.WriteLine($"using Newtonsoft.Json.Linq;");
                writer.WriteLine($"using Newtonsoft.Json.Serialization;");
                writer.WriteLine();
            }

            // Open the namespace.

            writer.WriteLine($"namespace {Settings.TargetNamespace}");
            writer.WriteLine($"{{");

            //---------------------------------------------
            // Generate the models.

            foreach (var dataModel in nameToDataModel.Values
                .OrderBy(dm => dm.SourceType.Name.ToLowerInvariant()))
            {
                GenerateDataModel(dataModel, genEntity: Settings.Persisted && dataModel.Persisted != null);
            }

            // Generate the service clients (if enabled).

            if (Settings.ServiceClients)
            {
                // Multiple service models may be combined into each generated
                // service client.  These are organized via the [ServiceModel.ClientTypeName]
                // property.  We're going to determine these groupings below.

                var clientNameToServiceModels = new Dictionary<string, List<ServiceModel>>();

                foreach (var serviceModel in nameToServiceModel.Values)
                {
                    if (!clientNameToServiceModels.TryGetValue(serviceModel.ClientTypeName, out var models))
                    {
                        clientNameToServiceModels.Add(serviceModel.ClientTypeName, models = new List<ServiceModel>());
                    }

                    models.Add(serviceModel);
                }

                // Generate the clients.

                foreach (var item in clientNameToServiceModels)
                {
                    GenerateServiceClient(item.Key, item.Value);
                }
            }

            // Close the namespace.

            writer.WriteLine($"}}");

            // Set the generated source code for the code generator output.

            Output.SourceCode = writer.ToString();
        }

        /// <summary>
        /// Determines whether a type is safe to use as a data model property.
        /// </summary>
        /// <param name="type">The type being checked.</param>
        /// <returns><c>true</c> if the type is safe.</returns>
        private bool IsSafeType(Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null);

            if (type == typeof(string))
            {
                // Special case this one.

                return true;
            }

            if (type.IsPrimitive || nameToDataModel.ContainsKey(type.FullName))
            {
                return true;
            }

            // NOTE: Value types (AKA struct) implicitly have a default parameterless constructor.

            if (type.Assembly.FullName.Contains("System.Private.CoreLib") &&
                type.IsValueType || type.GetConstructor(new Type[0]) != null)
            {
                return true;
            }

            // Arrays of the types meeting the criteria above are also allowed.

            if (type.IsArray)
            {
                var elementType = type.GetElementType();

                while (elementType.IsArray)
                {
                    elementType = elementType.GetElementType();
                }

                if (elementType.Assembly.FullName.Contains("System.Private.CoreLib") &&
                    elementType.IsValueType || elementType.GetConstructor(new Type[0]) != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Generates source code for a data model.
        /// </summary>
        /// <param name="dataModel">The data model.</param>
        /// <param name="genEntity">Optionally enables the generation of database persistence related code for this data model.</param>
        private void GenerateDataModel(DataModel dataModel, bool genEntity)
        {
            string          defaultValueExpression;
            PropertyInfo    persistedKeyProperty = null;

            if (genEntity && (dataModel.Persisted == null || dataModel.IsEnum))
            {
                // Nothing needs to be generated.

                return;
            }

            if (genEntity && !dataModel.IsEnum)
            {
                // We need to identify the data model property that acts as the
                // database key.

                foreach (var property in dataModel.SourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                {
                    if (property.GetCustomAttribute<PersistedKeyAttribute>() != null)
                    {
                        if (persistedKeyProperty != null)
                        {
                            Output.Error($"[{dataModel.SourceType.FullName}]: This data model has two properties [{persistedKeyProperty.Name}] and [{property.Name}] that are both tagged with [PersistedKey].  This is allowed for only one property per class.");
                            break;
                        }

                        persistedKeyProperty = property;
                    }
                }

                if (persistedKeyProperty == null)
                {
                    Output.Error($"[{dataModel.SourceType.FullName}]: This data model has no property tagged with [PersistedKey].  Entity classes must tag one property as the database key.");
                }
            }

            if (firstItemGenerated)
            {
                firstItemGenerated = false;
            }
            else
            {
                writer.WriteLine();
            }

            writer.WriteLine($"    //-------------------------------------------------------------------------");
            writer.WriteLine($"    // From: {dataModel.SourceType.FullName}");
            writer.WriteLine();

            if (dataModel.IsEnum)
            {
                if (dataModel.HasEnumFlags)
                {
                    writer.WriteLine($"    [Flags]");
                }

                writer.WriteLine($"    public enum {dataModel.SourceType.Name} : {dataModel.BaseTypeName}");
                writer.WriteLine($"    {{");

                foreach (var member in dataModel.EnumMembers)
                {
                    writer.WriteLine($"        [EnumMember(Value = \"{member.SerializedName}\")]");
                    writer.WriteLine($"        {member.Name} = {member.OrdinalValue},");
                }

                writer.WriteLine($"    }}");
            }
            else
            {
                var baseTypeRef = " : IGeneratedEntity";

                if (dataModel.IsDerived)
                {
                    if (!nameToDataModel.ContainsKey(dataModel.BaseTypeName))
                    {
                        Output.Error($"[{dataModel.SourceType.FullName}]: This data model inherits type [{dataModel.BaseTypeName}] which is not defined as a data model.");
                        return;
                    }

                    baseTypeRef = $" : {StripNamespace(dataModel.BaseTypeName)}, IGeneratedEntity";
                }
                else if (Settings.UxFeatures)
                {
                    baseTypeRef = " : __NotifyPropertyChanged";
                }

                if (genEntity)
                {
                    baseTypeRef += $", IPersistedEntity<{dataModel.SourceType.Name}>";
                }

                var className = dataModel.SourceType.Name;

                writer.WriteLine($"    /// <threadsafety static=\"true\" instance=\"false\"/>");
                writer.WriteLine($"    public partial class {className}{baseTypeRef}");
                writer.WriteLine($"    {{");

                if (Settings.RoundTrip)
                {
                    if (genEntity)
                    {
                        // We need to generate a custom Linq2Couchbase document filter attribute for
                        // each persisted data model.

                        writer.WriteLine($"        //---------------------------------------------------------------------");
                        writer.WriteLine($"        // Private types:");
                        writer.WriteLine();
                        writer.WriteLine($"        /// <summary>");
                        writer.WriteLine($"        /// Used to tag the <see cref=\"{className}\"/> entity such that Linq2Couchbase will");
                        writer.WriteLine($"        /// be able to transparently add a <c>where</c> clause that filters by entity type");
                        writer.WriteLine($"        /// to all queries for this entity type.");
                        writer.WriteLine($"        /// </summary>");
                        writer.WriteLine($"        private class {className}Filter : Couchbase.Linq.Filters.IDocumentFilter<{className}>");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            //-----------------------------------------------------------------");
                        writer.WriteLine($"            // Static members:");
                        writer.WriteLine();
                        writer.WriteLine($"            private static Expression<Func<{className}, bool>> whereExpression;");
                        writer.WriteLine();
                        writer.WriteLine($"            static {className}Filter()");
                        writer.WriteLine($"            {{");
                        writer.WriteLine($"                var parameter = Expression.Parameter(typeof({className}), \"p\");");
                        writer.WriteLine();
                        writer.WriteLine($"                whereExpression = Expression.Lambda<Func<{className}, bool>>(Expression.Equal(Expression.PropertyOrField(parameter, \"__ET\"), Expression.Constant({className}.EntityType)), parameter);");
                        writer.WriteLine($"            }}");
                        writer.WriteLine();
                        writer.WriteLine($"            //-----------------------------------------------------------------");
                        writer.WriteLine($"            // Instance members:");
                        writer.WriteLine();
                        writer.WriteLine($"            public int Priority {{ get; set; }}");
                        writer.WriteLine();
                        writer.WriteLine($"            public IQueryable<{className}> ApplyFilter(IQueryable<{className}> source)");
                        writer.WriteLine($"            {{");
                        writer.WriteLine($"                return source.Where(whereExpression);");
                        writer.WriteLine($"            }}");
                        writer.WriteLine($"        }}");
                        writer.WriteLine();
                    }

                    //-------------------------------------
                    // Generate the static members

                    writer.WriteLine($"        //---------------------------------------------------------------------");
                    writer.WriteLine($"        // Static members:");
                    writer.WriteLine();
                    writer.WriteLine($"        public const string EntityType = \"{dataModel.EntityType}\";");

                    if (genEntity)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        /// <summary>");
                        writer.WriteLine($"        /// Static constructor.");
                        writer.WriteLine($"        /// </summary>");
                        writer.WriteLine($"        static {className}()");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            // We need to register document filters with Linq2Couchbase.");
                        writer.WriteLine();
                        writer.WriteLine($"            Couchbase.Linq.Filters.DocumentFilterManager.SetFilter<{className}>(new {className}Filter());");
                        writer.WriteLine($"        }}");
                    }

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Deserializes an instance from JSON text.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"jsonText\">The JSON text input.</param>");
                    writer.WriteLine($"        /// <returns>The deserialized <see cref=\"{className}\"/>.</returns>");
                    writer.WriteLine($"        public static {className} CreateFrom(string jsonText)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (string.IsNullOrEmpty(jsonText))");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                throw new ArgumentNullException(nameof(jsonText));");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            var model = new {className}(EntitySerializationHelper.Deserialize<JObject>(jsonText));");
                    writer.WriteLine();
                    writer.WriteLine($"            model.__Load();");
                    writer.WriteLine($"            return model;");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Deserializes an instance from a <see cref=\"JObject\"/>.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"jObject\">The input <see cref=\"JObject\"/>.</param>");
                    writer.WriteLine($"        /// <returns>The deserialized <see cref=\"{className}\"/>.</returns>");
                    writer.WriteLine($"        public static {className} CreateFrom(JObject jObject)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (jObject == null)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                throw new ArgumentNullException(nameof(jObject));");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            var model = new {className}(jObject);");
                    writer.WriteLine();
                    writer.WriteLine($"            model.__Load();");
                    writer.WriteLine($"            return model;");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Deserializes an instance from a <see cref=\"Stream\"/>.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"stream\">The input <see cref=\"Stream\"/>.</param>");
                    writer.WriteLine($"        /// <param name=\"encoding\">Optionally specifies the inout encoding.  This defaults to <see cref=\"Encoding.UTF8\"/>.</param>");
                    writer.WriteLine($"        /// <returns>The deserialized <see cref=\"{className}\"/>.</returns>");
                    writer.WriteLine($"        public static {className} CreateFrom(Stream stream, Encoding encoding = null)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            encoding = encoding ?? Encoding.UTF8;");
                    writer.WriteLine();
                    writer.WriteLine($"            if (stream == null)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                throw new ArgumentNullException(nameof(stream));");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            {className} model;");
                    writer.WriteLine();
                    writer.WriteLine($"            using (var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true))");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                model = {className}.CreateFrom(reader.ReadToEnd());");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            model.__Load();");
                    writer.WriteLine($"            return model;");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Deserializes an instance from a <see cref=\"JsonResponse\"/>.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"response\">The input <see cref=\"JsonResponse\"/>.</param>");
                    writer.WriteLine($"        /// <returns>The deserialized <see cref=\"{className}\"/>.</returns>");
                    writer.WriteLine($"        public static {className} CreateFrom(JsonResponse response)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (response == null)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                throw new ArgumentNullException(nameof(response));");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            return CreateFrom(response.JsonText);");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Determines whether another entity instance has the same underlying type as this class.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"instance\">The instance to be tested or <c>null</c>.</param>");
                    writer.WriteLine($"        /// <returns>");
                    writer.WriteLine($"        /// <c>true</c> if the <paramref name=\"instance\"/> is not <c>null</c> and it has");
                    writer.WriteLine($"        /// the same type as the current class.");
                    writer.WriteLine($"        /// </returns>");
                    writer.WriteLine($"        public static bool SameTypeAs(IGeneratedEntity instance)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (instance == null)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                return false;");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            return instance.__ET == {className}.EntityType;");
                    writer.WriteLine($"        }}");

                    // For data models tagged with [Persisted], we need to generate the static GetKey(...) method.

                    if (genEntity)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        /// <summary>");
                        writer.WriteLine($"        /// Creates a key for an entity.");
                        writer.WriteLine($"        /// </summary>");
                        writer.WriteLine($"        /// <param name=\"args\">Arguments identifying the entity.</param>");
                        writer.WriteLine($"        public static string CreateKey(params object[] args)");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            return EntitySerializationHelper.CreateEntityKey(\"{dataModel.EntityType}\", args);");
                        writer.WriteLine($"        }}");
                    }

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Compares two instances for equality by performing a deep comparision of all object");
                    writer.WriteLine($"        /// properties including any hidden properties.  Note that you may pass <c>null</c>.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"value1\">The first value or <c>null</c>.</param>");
                    writer.WriteLine($"        /// <param name=\"value2\">The second value or <c>null</c>.</param>");
                    writer.WriteLine($"        /// <returns><c>true</c> if the values are equal.</returns>");
                    writer.WriteLine($"        public static bool operator ==({className} value1, {className} value2)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            var value1IsNull = object.ReferenceEquals(value1, null);");
                    writer.WriteLine($"            var value2IsNull = object.ReferenceEquals(value2, null);");
                    writer.WriteLine();
                    writer.WriteLine($"            if (value1IsNull == value2IsNull)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                if (value1IsNull)");
                    writer.WriteLine($"                {{");
                    writer.WriteLine($"                    return true;");
                    writer.WriteLine($"                }}");
                    writer.WriteLine($"                else");
                    writer.WriteLine($"                {{");
                    writer.WriteLine($"                    return value1.Equals(value2);");
                    writer.WriteLine($"                }}");
                    writer.WriteLine($"            }}");
                    writer.WriteLine($"            else");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                return false;");
                    writer.WriteLine($"            }}");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Compares two instances for inequality by performing a deep comparision of all object");
                    writer.WriteLine($"        /// properties including any hidden properties.  Note that you may pass <c>null</c>.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"value1\">The first value or <c>null</c>.</param>");
                    writer.WriteLine($"        /// <param name=\"value2\">The second value or <c>null</c>.</param>");
                    writer.WriteLine($"        /// <returns><c>true</c> if the values are not equal.</returns>");
                    writer.WriteLine($"        public static bool operator !=({className} value1, {className} value2)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            return !(value1 == value2);");
                    writer.WriteLine($"        }}");

                    //---------------------------------------------------------
                    // Generate instance members

                    writer.WriteLine();
                    writer.WriteLine($"        //---------------------------------------------------------------------");
                    writer.WriteLine($"        // Instance members:");
                    writer.WriteLine();
                    writer.WriteLine($"        private string cachedET;");

                    // Generate the backing __JObject property.

                    if (dataModel.BaseTypeName == null)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        /// <summary>");
                        writer.WriteLine($"        /// The backing <see cref=\"JObject\"/> used to hold the serialized data.");
                        writer.WriteLine($"        /// </summary>");
                        writer.WriteLine($"        protected JObject __JObject {{ get; set; }}");
                    }
                }

                // Generate the constructors.

                if (!dataModel.IsDerived)
                {
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Constructs an uninitialized instance.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        public {className}()");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            __JObject = new JObject();");
                    writer.WriteLine($"        }}");

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Protected constructor used internally to initialize derived classes.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"jObject\">The backing <see cref=\"JObject\"/>.</param>");
                    writer.WriteLine($"        protected {className}(JObject jObject)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            __JObject = jObject;");
                    writer.WriteLine($"        }}");
                }
                else
                {
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Constructs an uninitialized instance.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        public {className}() : base()");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"        }}");

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Protected constructor.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"jObject\">The backing <see cref=\"JObject\"/>.</param>");
                    writer.WriteLine($"        protected {className}(JObject jObject) : base(jObject)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"        }}");
                }

                // Generate the properties.

                foreach (var property in dataModel.Properties)
                {
                    writer.WriteLine();

                    if (property.Ignore)
                    {
                        writer.WriteLine($"        [JsonIgnore]");
                    }
                    else
                    {
                        var defaultValueHandling = string.Empty;

                        switch (property.DefaultValueHandling)
                        {
                            case DefaultValueHandling.Ignore:

                                defaultValueHandling = "Ignore";
                                break;

                            case DefaultValueHandling.IgnoreAndPopulate:

                                defaultValueHandling = "IgnoreAndPopulate";
                                break;

                            case DefaultValueHandling.Include:

                                defaultValueHandling = "Include";
                                break;

                            case DefaultValueHandling.Populate:

                                defaultValueHandling = "Populate";
                                break;

                            default:

                                Output.Error($"[{dataModel.SourceType.FullName}]: Service model [{property.Name}] specifies an unsupported [{nameof(DefaultValueHandling)}] value.");
                                defaultValueHandling = "Include";
                                break;
                        }

                        writer.WriteLine($"        [JsonProperty(PropertyName = \"{property.SerializedName}\", DefaultValueHandling = DefaultValueHandling.{defaultValueHandling}, Required = Required.{property.Required}, Order = {property.Order})]");

                        defaultValueExpression = property.DefaultValueExpression;

                        if (defaultValueExpression != null)
                        {
                            writer.WriteLine($"        [DefaultValue({defaultValueExpression})]");
                        }
                    }

                    var propertyTypeName = ResolveTypeReference(property.Type);

                    defaultValueExpression = property.DefaultValueExpression;

                    if (defaultValueExpression == null)
                    {
                        defaultValueExpression = string.Empty;
                    }
                    else
                    {
                        defaultValueExpression = $" = {defaultValueExpression};";
                    }

                    writer.WriteLine($"        public {propertyTypeName} {property.Name} {{ get; set; }}{defaultValueExpression}");
                }

                if (Settings.RoundTrip)
                {
                    //---------------------------------------------------------
                    // Generate the __Load() method.

                    var virtualModifier      = dataModel.IsDerived ? "override" : "virtual";
                    var serializedProperties = dataModel.Properties.Where(p => !p.Ignore);

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Loads the entity properties from the backing <see cref=\"JObject\"/>");
                    writer.WriteLine($"        /// or from the optional <see cref=\"JObject\"/> passed.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"source\">The optional source <see cref=\"JObject\"/>.</param>");
                    writer.WriteLine($"        /// <param name=\"isDerived\">Optionally indicates that were deserializing a derived class..</param>");
                    writer.WriteLine($"        public {virtualModifier} void __Load(JObject source = null, bool isDerived = false)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            JProperty property;");

                    if (serializedProperties.Count() > 0 || dataModel.IsDerived)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"            if (source != null)");
                        writer.WriteLine($"            {{");
                        writer.WriteLine($"                this.__JObject = source;");
                        writer.WriteLine($"            }}");

                        if (dataModel.IsDerived)
                        {
                            writer.WriteLine();
                            writer.WriteLine($"            base.__Load(isDerived: true);");
                        }

                        foreach (var property in serializedProperties.OrderBy(p => p.Order))
                        {
                            writer.WriteLine();

                            var resolvedPropertyType = ResolveTypeReference(property.Type);

                            writer.WriteLine($"            property = this.__JObject.Property(\"{property.SerializedName}\");");
                            writer.WriteLine($"            if (property != null)");
                            writer.WriteLine($"            {{");

                            if (property.RequiresObjectification)
                            {
                                writer.WriteLine($"                this.{property.Name} = property.Value.ToObject<{resolvedPropertyType}>(EntitySerializationHelper.Serializer);");
                            }
                            else
                            {
                                writer.WriteLine($"                this.{property.Name} = ({resolvedPropertyType})property.Value;");
                            }

                            writer.WriteLine($"            }}");

                            switch (property.DefaultValueHandling)
                            {
                                case DefaultValueHandling.Include:
                                case DefaultValueHandling.Ignore:

                                    // Doesn't impact deserialization.

                                    break;

                                case DefaultValueHandling.Populate:
                                case DefaultValueHandling.IgnoreAndPopulate:

                                    // Set the property to its default value, when the
                                    // default differs from the type's default.

                                    defaultValueExpression = property.DefaultValueExpression;

                                    if (defaultValueExpression != null)
                                    {
                                        writer.WriteLine($"            else");
                                        writer.WriteLine($"            {{");
                                        writer.WriteLine($"                this.{property.Name} = {defaultValueExpression};");
                                        writer.WriteLine($"            }}");
                                    }
                                    break;
                            }
                        }
                    }

                    // Load and verify the [__ET] property if we're not loading a derived class.

                    writer.WriteLine();
                    writer.WriteLine($"            if (!isDerived)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                property = this.__JObject.Property(\"__ET\");");
                    writer.WriteLine($"                if (property == null)");
                    writer.WriteLine($"                {{");
                    writer.WriteLine($"                    throw new ArgumentNullException(\"[{className}.__ET] property is required when deserializing.\");");
                    writer.WriteLine($"                }}");
                    writer.WriteLine($"                else");
                    writer.WriteLine($"                {{");
                    writer.WriteLine($"                    this.__ET = (string)property.Value;");
                    writer.WriteLine($"                }}");
                    writer.WriteLine($"            }}");

                    writer.WriteLine($"        }}");

                    //---------------------------------------------------------
                    // Generate the __Save() method.

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Persists the properties from this instance to the backing <see cref=\"JObject\"/>.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <returns>The backing <see cref=\"JObject\"/>.</returns>");
                    writer.WriteLine($"        public {virtualModifier} JObject __Save()");
                    writer.WriteLine($"        {{");

                    if (serializedProperties.Count() > 0 || dataModel.IsDerived)
                    {
                        writer.WriteLine($"            JProperty property;");
                        writer.WriteLine();

                        if (dataModel.IsDerived)
                        {
                            writer.WriteLine($"            base.__Save();");

                            if (dataModel.Properties.Count > 0)
                            {
                                writer.WriteLine();
                            }
                        }

                        var propertyIndex = 0;

                        foreach (var property in serializedProperties.OrderBy(p => p.Order))
                        {
                            if (property.Ignore)
                            {
                                continue;
                            }

                            var propertyTypeReference = ResolveTypeReference(property.Type);

                            switch (property.DefaultValueHandling)
                            {
                                case DefaultValueHandling.Include:
                                case DefaultValueHandling.Populate:

                                    if (property.RequiresObjectification)
                                    {
                                        writer.WriteLine($"            this.__JObject[\"{property.SerializedName}\"] = EntitySerializationHelper.FromObject(this.{property.Name}, typeof({className}), nameof({property.Name}));");
                                    }
                                    else
                                    {
                                        writer.WriteLine($"            this.__JObject[\"{property.SerializedName}\"] = this.{property.Name};");
                                    }
                                    break;

                                case DefaultValueHandling.Ignore:
                                case DefaultValueHandling.IgnoreAndPopulate:

                                    if (propertyIndex++ > 0)
                                    {
                                        writer.WriteLine();
                                    }

                                    defaultValueExpression = property.DefaultValueExpression ?? "default";

                                    writer.WriteLine($"            if (this.{property.Name} == {defaultValueExpression})");
                                    writer.WriteLine($"            {{");
                                    writer.WriteLine($"                if (this.__JObject.Property(\"{property.SerializedName}\") != null)");
                                    writer.WriteLine($"                 {{");
                                    writer.WriteLine($"                    this.__JObject.Remove(\"{property.SerializedName}\");");
                                    writer.WriteLine($"                }}");
                                    writer.WriteLine($"            }}");
                                    writer.WriteLine($"            else");
                                    writer.WriteLine($"            {{");

                                    if (property.RequiresObjectification)
                                    {
                                        writer.WriteLine($"                this.__JObject[\"{property.SerializedName}\"] = EntitySerializationHelper.FromObject(this.{property.Name}, typeof({className}), nameof({property.Name}));");
                                    }
                                    else
                                    {
                                        writer.WriteLine($"                this.__JObject[\"{property.SerializedName}\"] = this.{property.Name};");
                                    }

                                    writer.WriteLine($"            }}");
                                    break;
                            }
                        }
                    }

                    // Serialize the [__ET] property

                    writer.WriteLine($"            this.__JObject[\"__ET\"] = EntityType;");

                    writer.WriteLine();
                    writer.WriteLine($"            return this.__JObject;");
                    writer.WriteLine($"        }}");

                    //---------------------------------------------------------
                    // Generate the ToString() methods.

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Renders the instance as JSON text.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <returns>The serialized JSON string.</returns>");
                    writer.WriteLine($"        public override string ToString()");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            __Save();");
                    writer.WriteLine($"            return EntitySerializationHelper.Serialize(__JObject, Formatting.None);");
                    writer.WriteLine($"        }}");

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Renders the instance as JSON text, optionally formatting the output.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"indented\">Optionally pass <c>true</c> to format the output.</param>");
                    writer.WriteLine($"        /// <returns>The serialized JSON string.</returns>"); 
                    writer.WriteLine($"        public string ToString(bool indented)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            __Save();");
                    writer.WriteLine($"            return EntitySerializationHelper.Serialize(__JObject, indented ? Formatting.Indented : Formatting.None);");
                    writer.WriteLine($"        }}");

                    //-------------------------------------
                    // Generate the ToJObject() method if this is the root class.

                    if (!dataModel.IsDerived)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        /// <summary>");
                        writer.WriteLine($"        /// Renders the instances as a <see cref=\"JObject\"/>.");
                        writer.WriteLine($"        /// </summary>");
                        writer.WriteLine($"        /// <param name=\"noClone\">Optionally return the underlying <see cref=\"JObject\"/> without cloning it for better performance.</param>");
                        writer.WriteLine($"        /// <returns>The underlying <see cref=\"JObject\"/> (cloned by default).</returns>");
                        writer.WriteLine($"        public JObject ToJObject(bool noClone = false)");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            __Save();");
                        writer.WriteLine($"            return noClone ? __JObject : EntitySerializationHelper.DeepClone(__JObject);");
                        writer.WriteLine($"        }}");
                    }

                    //---------------------------------------------------------
                    // Generate handy helper methods.

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Returns a deep clone of the instance.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <returns>The cloned instance.</returns>");
                    writer.WriteLine($"        public {className} DeepClone()");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            __Save();");
                    writer.WriteLine($"            return CreateFrom(EntitySerializationHelper.DeepClone(__JObject));");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Used to convert a base data model class into a derived class.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <typeparam name=\"T\">The desired derived type.</typeparam>");
                    writer.WriteLine($"        /// <param name=\"noClone\">");
                    writer.WriteLine($"        /// By default, this method will create a deep clone of the underlying <see cref=\"JObject\"/>");
                    writer.WriteLine($"        /// and use this new instance when constructing the new object.  This is the safest");
                    writer.WriteLine($"        /// approach but will cause a performance hit.  You can pass <paramref name=\"noClone\"/><c>=true</c>");
                    writer.WriteLine($"        /// to reuse the existing <see cref=\"JObject\"/> for the new instance if you're sure that the");
                    writer.WriteLine($"        /// original instance will no longer be accessed.");
                    writer.WriteLine($"        /// </param>");
                    writer.WriteLine($"        /// <returns>The converted instance of type <typeparamref name=\"T\"/>.</returns>");
                    writer.WriteLine($"        public T ToDerived<T>(bool noClone = false)");
                    writer.WriteLine($"           where T : {className}, IGeneratedEntity");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            __Save();");
                    writer.WriteLine($"            return GeneratedEntityFactory.CreateFrom<T>(noClone ? __JObject : EntitySerializationHelper.DeepClone(__JObject));");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Determines whether the current instance equals another object.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <param name=\"obj\">The other object instance or <c>null</c>.</param>");
                    writer.WriteLine($"        /// <returns><c>true</c> if the object reference equals the current instance.</returns>");
                    writer.WriteLine($"        public override bool Equals(object obj)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (object.ReferenceEquals(this, obj))");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                return true;");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            var other = obj as {className};");
                    writer.WriteLine();
                    writer.WriteLine($"            if (object.ReferenceEquals(other, null))");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                return false;");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            this.__Save();");
                    writer.WriteLine($"            other.__Save();");
                    writer.WriteLine($"            return JObject.DeepEquals(this.__JObject, other.__JObject);");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Calculates the hash code for the instance.");
                    writer.WriteLine($"        /// <note>");
                    writer.WriteLine($"        /// At least one of the class properties must be tagged with a <b>[HashSource]</b>");
                    writer.WriteLine($"        /// for this to work.");
                    writer.WriteLine($"        /// </note>");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        /// <returns>The calculated hash code.</returns>");
                    writer.WriteLine($"        /// <exception cref=\"InvalidOperationException\">Thrown when no class properties are tagged with a <c>[HashSourceAttribute]</c>.</exception>");
                    writer.WriteLine($"        public override int GetHashCode()");
                    writer.WriteLine($"        {{");

                    // This is implemented by looking for all of the properties
                    // tagged by [HashSource], incuding any inherited properties
                    // and generating the hash code from these sorted in ascending
                    // order by serialized property name.  This should provide for
                    // relatively consistent hash code computations over time.
                    //
                    // Note that we require at least one tagged [HashSource]
                    // property.

                    var hashedProperties = dataModel.SelectProperties(p => p.IsHashSource, includeInherited: true).ToList();

                    if (hashedProperties.Count == 0)
                    {
                        writer.WriteLine($"            throw new InvalidOperationException(EntitySerializationHelper.NoHashPropertiesError);");
                    }
                    else
                    {
                        writer.WriteLine($"            var hashCode = 0;");
                        writer.WriteLine();

                        foreach (var property in hashedProperties)
                        {
                            if (property.Type.IsValueType)
                            {
                                writer.WriteLine($"            hashCode ^= this.{property.Name}.GetHashCode();");
                            }
                            else
                            {
                                writer.WriteLine($"            if (this.{property.Name} != null) {{ hashCode ^= this.{property.Name}.GetHashCode(); }}");
                            }
                        }

                        writer.WriteLine();
                        writer.WriteLine($"            return hashCode;");
                    }

                    writer.WriteLine($"        }}");

                    //---------------------------------------------------------
                    // Generate the entity type property.

                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary>");
                    writer.WriteLine($"        /// Identifies the entity type.");
                    writer.WriteLine($"        /// </summary>");
                    writer.WriteLine($"        public string __ET");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            get");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                 if (cachedET != null)");
                    writer.WriteLine($"                 {{");
                    writer.WriteLine($"                     return cachedET;");
                    writer.WriteLine($"                 }}");
                    writer.WriteLine();
                    writer.WriteLine($"                 cachedET = (string)__JObject[\"__ET\"];");
                    writer.WriteLine();
                    writer.WriteLine($"                 if (cachedET != null)");
                    writer.WriteLine($"                 {{");
                    writer.WriteLine($"                     return cachedET;");
                    writer.WriteLine($"                 }}");
                    writer.WriteLine();
                    writer.WriteLine($"                 return EntityType;");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            set => cachedET = value;");
                    writer.WriteLine($"        }}");

                    //---------------------------------------------------------
                    // Generate any entity related members.

                    if (genEntity)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        /// <summary>");
                        writer.WriteLine($"        /// Returns the database key for an entity.");
                        writer.WriteLine($"        /// </summary>");
                        writer.WriteLine($"        /// <param name=\"args\">Arguments identifying the entity.</param>");
                        writer.WriteLine($"        public string GetKey()");
                        writer.WriteLine($"        {{");

                        if (persistedKeyProperty == null)
                        {
                            writer.WriteLine($"            return null; // ERROR: No source data model property was tagged by [PersistedKey].");
                        }
                        else if (persistedKeyProperty.PropertyType.IsValueType)
                        {
                            writer.WriteLine($"            return {persistedKeyProperty.Name}.ToString();");
                        }
                        else
                        {
                            writer.WriteLine($"            if ({persistedKeyProperty.Name} == null)");
                            writer.WriteLine($"            {{");
                            writer.WriteLine($"                throw new InvalidOperationException(\"Entity key property [{persistedKeyProperty.Name}] cannot be NULL.\");");
                            writer.WriteLine($"            }}");
                            writer.WriteLine();

                            if (persistedKeyProperty.PropertyType == typeof(string))
                            {
                                writer.WriteLine($"            return {persistedKeyProperty.Name};");
                            }
                            else
                            {
                                writer.WriteLine($"            return {persistedKeyProperty.Name}.ToString();");
                            }
                        }

                        writer.WriteLine($"        }}");
                    }

                    // Close the generated model class definition.

                    writer.WriteLine($"    }}");
                }
            }
        }

        /// <summary>
        /// Generates a service client for a one or more related service controllers.
        /// </summary>
        /// <param name="clientTypeName">The client type name.</param>
        /// <param name="serviceModels">One or more service models to be included in the generated output.</param>
        private void GenerateServiceClient(string clientTypeName, IEnumerable<ServiceModel> serviceModels)
        {
            Covenant.Requires<ArgumentNullException>(serviceModels != null);
            Covenant.Requires<ArgumentException>(serviceModels.Any());

            // Ensure that all of the service models have the same client name.

            var clientNameSet = new HashSet<string>();

            foreach (var serviceModel in serviceModels)
            {
                if (!clientNameSet.Contains(serviceModel.ClientTypeName))
                {
                    clientNameSet.Add(serviceModel.ClientTypeName);
                }
            }

            Covenant.Assert(clientNameSet.Count > 0);

            // Service models may be organized into zero or more client groups by client
            // group name.  Service methods that are not within a client group will be
            // generated directly within the class.  Methods within a client group will
            // be generated in subclasses within the client class.
            //
            // We're going collate the service methods into client groups by name,
            // with the empty name referring to methods that should appear directly
            // within the generated service class.

            var clientGroups = new Dictionary<string, List<ServiceMethod>>();

            foreach (var serviceModel in serviceModels)
            {
                var groupName = serviceModel.ClientGroup ?? string.Empty;

                if (!clientGroups.TryGetValue(groupName, out var clientGroup))
                {
                    clientGroups.Add(groupName, clientGroup = new List<ServiceMethod>());
                }

                foreach (var serviceMethod in serviceModel.Methods)
                {
                    clientGroup.Add(serviceMethod);
                }
            }

            var rootMethodGroups       = clientGroups.Where(cg => string.IsNullOrEmpty(cg.Key));
            var nonRootMethodGroups    = clientGroups.Where(cg => !string.IsNullOrEmpty(cg.Key));
            var hasNonRootMethodGroups = nonRootMethodGroups.Any();

            // $todo(jeff.lill):
            //
            // Generate the class and method comments below by parsing any code documentation.

            if (firstItemGenerated)
            {
                firstItemGenerated = false;
            }
            else
            {
                writer.WriteLine();
            }

            writer.WriteLine($"    //-------------------------------------------------------------------------");

            foreach (var serviceModel in serviceModels)
            {
                writer.WriteLine($"    // From: {serviceModel.SourceType.FullName}");
            }

            writer.WriteLine();
            writer.WriteLine($"    public partial class {clientTypeName} : IDisposable");
            writer.WriteLine($"    {{");

            if (hasNonRootMethodGroups)
            {
                // Generate local [class] definitions for any non-root service
                // methods here.

                foreach (var clientGroup in nonRootMethodGroups)
                {
                    writer.WriteLine($"        public class __{clientGroup.Key}");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            private JsonClient client;");
                    writer.WriteLine();
                    writer.WriteLine($"            internal __{clientGroup.Key}(JsonClient client)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                this.client = client;");
                    writer.WriteLine($"            }}");

                    foreach (var serviceMethod in clientGroup.Value)
                    {
                        GenerateServiceMethod(serviceMethod, indent: "    ");
                    }

                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                }
            }

            writer.WriteLine($"        private JsonClient   client;");
            writer.WriteLine($"        private bool         isDisposed = false;");
            writer.WriteLine();
            writer.WriteLine($"        /// <summary>");
            writer.WriteLine($"        /// Constructor.");
            writer.WriteLine($"        /// </summary>");
            writer.WriteLine($"        /// <param name=\"handler\">An optional message handler.  This defaults to a reasonable handler with compression enabled.</param>");
            writer.WriteLine($"        /// <param name=\"disposeHandler\">Indicates whether the handler passed will be disposed automatically (defaults to <c>false</c>).</param>");
            writer.WriteLine($"        public {clientTypeName}(HttpMessageHandler handler = null, bool disposeHandler = false)");
            writer.WriteLine($"        {{");
            writer.WriteLine($"            this.client = new JsonClient(handler, disposeHandler);");

            if (hasNonRootMethodGroups)
            {
                // Initialize the non-root method group properties.

                foreach (var nonRootGroup in nonRootMethodGroups)
                {
                    writer.WriteLine($"            this.{nonRootGroup.Key} = new __{nonRootGroup.Key}(this.client);");
                }
            }

            writer.WriteLine($"        }}");
            writer.WriteLine();
            writer.WriteLine($"        /// <summary");
            writer.WriteLine($"        /// Finalizer.");
            writer.WriteLine($"        /// </summary");
            writer.WriteLine($"        ~{clientTypeName}()");
            writer.WriteLine($"        {{");
            writer.WriteLine($"             Dispose(false);");
            writer.WriteLine($"        }}");
            writer.WriteLine();
            writer.WriteLine($"        /// <inheritdoc/>");
            writer.WriteLine($"        public void Dispose()");
            writer.WriteLine($"        {{");
            writer.WriteLine($"        }}");
            writer.WriteLine();
            writer.WriteLine($"        /// <summary>");
            writer.WriteLine($"        /// Releases any important resources associated with the instance.");
            writer.WriteLine($"        /// </summary>");
            writer.WriteLine($"        /// <param name=\"disposing\">Pass <c>true</c> if the instance is being disposed as opposed to being finalized.</param>");
            writer.WriteLine($"        protected void Dispose(bool disposing)");
            writer.WriteLine($"        {{");
            writer.WriteLine($"            if (isDisposed)");
            writer.WriteLine($"            {{");
            writer.WriteLine($"                return;");
            writer.WriteLine($"            }}");
            writer.WriteLine();
            writer.WriteLine($"            client.Dispose();");
            writer.WriteLine();
            writer.WriteLine($"            if (disposing)");
            writer.WriteLine($"            {{");
            writer.WriteLine($"                GC.SuppressFinalize(this);");
            writer.WriteLine($"            }}");
            writer.WriteLine();
            writer.WriteLine($"            isDisposed = true;");
            writer.WriteLine($"        }}");
            writer.WriteLine();
            writer.WriteLine($"        /// <summary");
            writer.WriteLine($"        /// Returns the underlying <see cref=\"JsonClient\"/>.");
            writer.WriteLine($"        /// </summary");
            writer.WriteLine($"        public JsonClient JsonClient => client;");
            writer.WriteLine();
            writer.WriteLine($"        /// <summary");
            writer.WriteLine($"        /// Returns the underlying <see cref=\"HttpClient\"/>.");
            writer.WriteLine($"        /// </summary");
            writer.WriteLine($"        public HttpClient HttpClient => client.HttpClient;");
            writer.WriteLine();
            writer.WriteLine($"        /// <summary");
            writer.WriteLine($"        /// Accesses the underlying <see cref=\"HttpClient.Timeout\"/>.");
            writer.WriteLine($"        /// </summary");
            writer.WriteLine($"        public TimeSpan Timeout");
            writer.WriteLine($"        {{");
            writer.WriteLine($"            get => client.Timeout;");
            writer.WriteLine($"            set => client.Timeout = value;");
            writer.WriteLine($"        }}");
            writer.WriteLine();
            writer.WriteLine($"        /// <summary");
            writer.WriteLine($"        /// Accesses the underlying <see cref=\"HttpClient.BaseAddress\"/>.");
            writer.WriteLine($"        /// </summary");
            writer.WriteLine($"        public Uri BaseAddress");
            writer.WriteLine($"        {{");
            writer.WriteLine($"            get => client.BaseAddress;");
            writer.WriteLine($"            set => client.BaseAddress = value;");
            writer.WriteLine($"        }}");
            writer.WriteLine();
            writer.WriteLine($"        /// <summary");
            writer.WriteLine($"        /// Returns the underlying <see cref=\"HttpClient.DefaultRequestHeaders\"/>.");
            writer.WriteLine($"        /// </summary");
            writer.WriteLine($"        public HttpRequestHeaders DefaultRequestHeaders => client.DefaultRequestHeaders;");

            if (hasNonRootMethodGroups)
            {
                // Generate any service group properties.

                foreach (var nonRootGroup in nonRootMethodGroups)
                {
                    writer.WriteLine();
                    writer.WriteLine($"        /// <summary");
                    writer.WriteLine($"        /// <b>{nonRootGroup.Key}</b> related service methods.");
                    writer.WriteLine($"        /// </summary");
                    writer.WriteLine($"        public __{nonRootGroup.Key} {nonRootGroup.Key} {{ get; private set; }}");
                }
            }

            // Generate any root service methods here.

            foreach (var rootGroup in rootMethodGroups)
            {
                foreach (var serviceMethod in rootGroup.Value)
                {
                    GenerateServiceMethod(serviceMethod);
                }
            }

            writer.WriteLine($"    }}");
        }

        /// <summary>
        /// Generates a service's method code.
        /// </summary>
        /// <param name="serviceMethod">The service method.</param>
        /// <param name="indent">Optionally specifies additional source code indentation.</param>
        private void GenerateServiceMethod(ServiceMethod serviceMethod, string indent = "")
        {
            // Verify that the method result type is reasonable.

            if (!IsValidMethodType(serviceMethod.MethodInfo.ReturnType, Pass.AsResult))
            {
                Output.Error($"Service method [{serviceMethod.ServiceModel.SourceType.Name}.{serviceMethod.Name}(...)] returns unsupported type [{serviceMethod.MethodInfo.ReturnType}].");
            }

            var parameters    = serviceMethod.Parameters;
            var bodyParameter = parameters.FirstOrDefault(p => p.Pass == Pass.AsBody);

            //-----------------------------------------------------------------
            // Common code that applies to both of the generated [save] and [unsafe] methods.

            // Generate the method parameter definition.

            var argSeparator = ", ";
            var sbParameters = new StringBuilder();

            foreach (var parameter in serviceMethod.MethodInfo.GetParameters())
            {
                sbParameters.AppendWithSeparator($"{ResolveTypeReference(parameter.ParameterType)} {parameter.Name}", argSeparator);
            }

            sbParameters.AppendWithSeparator("CancellationToken cancellationToken = default", argSeparator);
            sbParameters.AppendWithSeparator("IRetryPolicy retryPolicy = default", argSeparator);
            sbParameters.AppendWithSeparator("LogActivity logActivity = default", argSeparator);

            // Generate the arguments to be passed to the query methods.

            var sbArgGenerate    = new StringBuilder();   // Will hold the code required to generate the arguments.
            var sbArguments      = new StringBuilder();   // Will hold the arguments to be passed to the [JsonClient] method.
            var routeParameters  = new List<MethodParameter>();
            var headerParameters = parameters.Where(p => p.Pass == Pass.AsHeader);
            var uriRef           = $"\"{serviceMethod.RouteTemplate}\"";

            foreach (var routeParameter in parameters.Where(p => p.Pass == Pass.InRoute))
            {
                routeParameters.Add(routeParameter);
            }

            if (!string.IsNullOrEmpty(serviceMethod.RouteTemplate))
            {
                // NOTE:
                //
                // When a service method has a [Route] attribute that defines a route template, 
                // we're going to treat any parameter not tagged with a [FromXXX] attribute as
                // if it is tagged by a [FromRoute] attribute when the parameter name matches
                // a reference in the route template.

                // Extract the parameter names from the route template.

                var templateParameters = new HashSet<string>();

                foreach (Match match in routeParameterRegex.Matches(serviceMethod.RouteTemplate))
                {
                    var param = match.Groups[1].Value;

                    if (!templateParameters.Contains(param))
                    {
                        templateParameters.Add(param);
                    }
                }

                // Compare the parameters without a [FromXXX] attribute against the
                // the route template parameters by name, assigning [Pass.InRoute]
                // to any of these.
                // 
                // NOTE: Parameters will be assigned [Pass.InQuery] by default when
                //       no  [FromXXX] tag was present, so all we need to do is to
                //       check for the absence of a [FromQuery] attribute.

                foreach (var parameter in serviceMethod.Parameters)
                {
                    var noFromXXX = parameter.ParameterInfo.GetCustomAttribute<FromQueryAttribute>() == null;

                    if (noFromXXX && templateParameters.Contains(parameter.SerializedName))
                    {
                        parameter.Pass = Pass.InRoute;

                        routeParameters.Add(parameter);
                    }
                }
            }

            // The query parameters include those that have [Pass.InQuery].

            var queryParameters = parameters.Where(p => p.Pass == Pass.InQuery);

            // We're ready to generate the method code.

            if (!string.IsNullOrEmpty(serviceMethod.RouteTemplate))
            {
                // NOTE:
                //
                // When a service method has a [Route] attribute that defines a route template, 
                // we're going to treat any parameter that has no other [FromXXX] attribute as
                // if it was tagged by a [FromRoute] attribute.

                if (sbArgGenerate.Length > 0)
                {
                    sbArgGenerate.AppendLine();
                }

                var route     = serviceMethod.RouteTemplate;
                var uri       = route;
                var uriVerify = route;

                foreach (var parameter in routeParameters)
                {
                    if (!serviceMethod.RouteTemplate.Contains($"{{{parameter.SerializedName}}}"))
                    {
                        Output.Error($"Service method [{serviceMethod.ServiceModel.SourceType.Name}.{serviceMethod.Name}(...)] has parameter [{parameter.Name}] that does not map to a [{{{parameter.Name}}}] in the method's [{serviceMethod.RouteTemplate}] route template.");
                    }

                    uriVerify = uriVerify.Replace($"{{{parameter.SerializedName}}}", parameter.Name);

                    // Generate the URI template parameter.  These need to be URI encoded and
                    // note that we also need to treat Enum parameters specially to ensure that 
                    // they honor any [EnumMember] attributes.

                    if (parameter.ParameterInfo.ParameterType.IsEnum)
                    {
                        uri = uri.Replace($"{{{parameter.SerializedName}}}", $"{{Uri.EscapeUriString(NeonHelper.EnumToString({parameter.Name}))}}");
                    }
                    else
                    {
                        if (parameter.ParameterInfo.ParameterType == typeof(string))
                        {
                            uri = uri.Replace($"{{{parameter.SerializedName}}}", $"{{Uri.EscapeUriString({parameter.Name})}}");
                        }
                        else
                        {
                            uri = uri.Replace($"{{{parameter.SerializedName}}}", $"{{Uri.EscapeUriString({parameter.Name}.ToString())}}");
                        }
                    }
                }

                if (uriVerify.Contains('{') || uriVerify.Contains('}'))
                {
                    Output.Error($"Service method [{serviceMethod.ServiceModel.SourceType.Name}.{serviceMethod.Name}(...)] has a malformed route [{route}] that references a method parameter that doesn't exist or has extra \"{{\" or \"}}\" characters.");
                }

                uriRef = $"$\"{uri}\"";
            }

            if (queryParameters.Count() > 0)
            {
                if (sbArgGenerate.Length > 0)
                {
                    sbArgGenerate.AppendLine();
                }

                sbArgGenerate.AppendLine($"{indent}            var args = new ArgDictionary()");
                sbArgGenerate.AppendLine($"{indent}            {{");

                foreach (var parameter in queryParameters)
                {
                    if (parameter.ParameterInfo.ParameterType.IsEnum)
                    {
                        sbArgGenerate.AppendLine($"{indent}                {{ \"{parameter.SerializedName}\", NeonHelper.EnumToString({parameter.Name}) }},");
                    }
                    else
                    {
                        sbArgGenerate.AppendLine($"{indent}                {{ \"{parameter.SerializedName}\", {parameter.Name} }},");
                    }
                }

                sbArgGenerate.AppendLine($"{indent}            }};");
            }

            if (headerParameters.Count() > 0)
            {
                if (sbArgGenerate.Length > 0)
                {
                    sbArgGenerate.AppendLine();
                }

                sbArgGenerate.AppendLine($"{indent}            var headers = new ArgDictionary()");
                sbArgGenerate.AppendLine($"{indent}            {{");

                foreach (var parameter in headerParameters)
                {
                    sbArgGenerate.AppendLine($"{indent}                {{ \"{parameter.SerializedName}\", {parameter.Name} }},");
                }

                sbArgGenerate.AppendLine($"{indent}            }};");
            }

            sbArguments.AppendWithSeparator("retryPolicy ?? NoRetryPolicy.Instance", argSeparator);
            sbArguments.AppendWithSeparator(uriRef, argSeparator);

            if (bodyParameter != null)
            {
                if (nameToDataModel.ContainsKey(bodyParameter.ParameterInfo.ParameterType.FullName))
                {
                    sbArguments.AppendWithSeparator($"document: {bodyParameter.Name}.ToString()", argSeparator);
                }
                else
                {
                    sbArguments.AppendWithSeparator($"document: EntitySerializationHelper.Serialize({bodyParameter.Name})", argSeparator);
                }
            }

            if (queryParameters.Count() > 0)
            {
                sbArguments.AppendWithSeparator("args: args", argSeparator);
            }

            if (headerParameters.Count() > 0)
            {
                sbArguments.AppendWithSeparator("headers: headers", argSeparator);
            }

            sbArguments.AppendWithSeparator("cancellationToken: cancellationToken", argSeparator);
            sbArguments.AppendWithSeparator("logActivity: logActivity", argSeparator);

            // Generate the safe and unsafe query method names and 
            // verify that each method actually supports sending
            // any [FromBody] object.

            var methodReturnsContent = true;
            var safeQueryMethod      = string.Empty;
            var unsafeQueryMethod    = string.Empty;
            var bodyError            = $"Service method [{serviceMethod.ServiceModel.SourceType.Name}.{serviceMethod.Name}(...)] defines a parameter with [FromBody] that is not compatible with the HTTP [{serviceMethod.HttpMethod}] method.";

            switch (serviceMethod.HttpMethod)
            {
                case "DELETE":

                    safeQueryMethod   = "DeleteAsync";
                    unsafeQueryMethod = "DeleteUnsafeAsync";

                    if (bodyParameter != null)
                    {
                        Output.Error(bodyError);
                    }
                    break;

                case "GET":

                    safeQueryMethod   = "GetAsync";
                    unsafeQueryMethod = "GetUnsafeAsync";

                    if (bodyParameter != null)
                    {
                        Output.Error(bodyError);
                    }
                    break;

                case "HEAD":

                    methodReturnsContent = false;

                    safeQueryMethod   = "HeadAsync";
                    unsafeQueryMethod = "HeadUnsafeAsync";

                    if (bodyParameter != null)
                    {
                        Output.Error(bodyError);
                    }
                    break;

                case "OPTIONS":

                    safeQueryMethod   = "OptionsAsync";
                    unsafeQueryMethod = "OptionsUnsafeAsync";

                    if (bodyParameter != null)
                    {
                        Output.Error(bodyError);
                    }
                    break;

                case "PATCH":

                    safeQueryMethod   = "PatchAsync";
                    unsafeQueryMethod = "PatchUnsafeAsync";
                    break;

                case "POST":

                    safeQueryMethod   = "PostAsync";
                    unsafeQueryMethod = "PostUnsafeAsync";
                    break;

                case "PUT":

                    safeQueryMethod   = "PutAsync";
                    unsafeQueryMethod = "PutUnsafeAsync";
                    break;

                default:

                    throw new NotImplementedException($"HTTP method [{serviceMethod.HttpMethod}] support is not implemented.");
            }

            //-----------------------------------------------------------------
            // Generate the [safe] version of the method.

            var returnType       = ResolveTypeReference(serviceMethod.MethodInfo.ReturnType, out var resultIsDataModel, isResultType: true);
            var methodReturnType = returnType;

            if (serviceMethod.IsVoid || !methodReturnsContent)
            {
                methodReturnType = "Task";
            }
            else
            {
                methodReturnType = $"Task<{methodReturnType}>";
            }

            var methodName = serviceMethod.Name;

            if (!methodName.EndsWith("Async"))
            {
                methodName += "Async";
            }

            writer.WriteLine();
            writer.WriteLine($"{indent}        public async {methodReturnType} {methodName}({sbParameters})");
            writer.WriteLine($"{indent}        {{");

            if (sbArgGenerate.Length > 0)
            {
                writer.WriteLine(sbArgGenerate);
            }

            if (serviceMethod.IsVoid || !methodReturnsContent)
            {
                writer.WriteLine($"{indent}            await client.{safeQueryMethod}({sbArguments});");
            }
            else
            {
                if (resultIsDataModel && !serviceMethod.MethodInfo.ReturnType.IsEnum)
                {
                    writer.WriteLine($"{indent}            return {returnType}.CreateFrom(await client.{safeQueryMethod}({sbArguments}));");
                }
                else
                {
                    writer.WriteLine($"{indent}            return (await client.{safeQueryMethod}({sbArguments})).As<{returnType}>();");
                }
            }

            writer.WriteLine($"{indent}        }}");

            //-----------------------------------------------------------------
            // Generate the [unsafe] version of the method.

            writer.WriteLine();
            writer.WriteLine($"{indent}        public async Task<JsonResponse> Unsafe{methodName}({sbParameters})");
            writer.WriteLine($"{indent}        {{");

            if (sbArgGenerate.Length > 0)
            {
                writer.WriteLine(sbArgGenerate);
            }

            writer.WriteLine($"{indent}            return await client.{unsafeQueryMethod}({sbArguments});");
            writer.WriteLine($"{indent}        }}");
        }

        /// <summary>
        /// Determines whether a type can be used as a service method parameter
        /// or result.
        /// </summary>
        /// <param name="type">The type being tested.</param>
        /// <param name="pass">Indicates how the value will be serialized.</param>
        /// <returns><c>true</c> if the type is valid.</returns>
        private bool IsValidMethodType(Type type, Pass pass)
        {
            if (pass == Pass.AsResult)
            {
                return ResolveTypeReference(type, isResultType: true) != null;
            }

            // By default, parameters may only be a primitive type, a string, or an enum.
            // The only exception is for parameters passed as the request body.

            if (pass == Pass.AsBody)
            {
                return ResolveTypeReference(type) != null;
            }
            else
            {
                return type.IsPrimitive || type == typeof(string) || type.IsEnum;
            }
        }

        /// <summary>
        /// Returns the name we'll use for a type when generating type references.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The type name.</returns>
        private string GetTypeName(Type type)
        {
            // Convert common types into their C# equivents:

            var typeName = type.FullName;

            switch (typeName)
            {
                case "System.Byte":     return "byte";
                case "System.SByte":    return "sbyte";
                case "System.Int16":    return "short";
                case "System.UInt16":   return "ushort";
                case "System.Int32":    return "int";
                case "System.UInt32":   return "uint";
                case "System.Int64":    return "long";
                case "System.UInt64":   return "ulong";
                case "System.Float":    return "float";
                case "System.Double":   return "double";
                case "System.String":   return "string";
                case "System.Boolean":  return "bool";
                case "System.Decimal":  return "decimal";
            }

            if (type.IsGenericType)
            {
                // Strip the backtick and any text after it.

                var tickPos = typeName.IndexOf('`');

                if (tickPos != -1)
                {
                    typeName = typeName.Substring(0, tickPos);
                }
            }

            if (nameToDataModel.ContainsKey(type.FullName))
            {
                // Strip the namespace off of local data model types.

                typeName = StripNamespace(typeName);
            }

            return typeName;
        }

        /// <summary>
        /// Strips the namespace (if present) from a type name.
        /// </summary>
        /// <param name="typeName">The type name.</param>
        /// <returns>The type name without the namespace.</returns>
        private string StripNamespace(string typeName)
        {
            if (typeName == null)
            {
                return null;
            }

            var lastDotPos = typeName.LastIndexOf('.');

            if (lastDotPos != -1)
            {
                return typeName.Substring(lastDotPos + 1);
            }
            else
            {
                return typeName;
            }
        }

        /// <summary>
        /// Resolves the type passed into a nice string taking generic types 
        /// and arrays into account.
        /// </summary>
        /// <param name="type">The referenced type.</param>
        /// <param name="isResultType">Optionally allow the <c>void</c> and related types (used for service method results).</param>
        /// <returns>The type reference as a string or <c>null</c> if the type is not valid.</returns>
        private string ResolveTypeReference(Type type, bool isResultType = false)
        {
            return ResolveTypeReference(type, out var ignoredd, isResultType);
        }

        /// <summary>
        /// Resolves the type passed into a nice string taking generic types 
        /// and arrays into account.
        /// </summary>
        /// <param name="type">The referenced type.</param>
        /// <param name="isModelType">Returns <c>true</c> if the type is a defined data model.</param>
        /// <param name="isResultType">Optionally allow the <c>void</c> and related types (used for service method results).</param>
        /// <returns>The type reference as a string or <c>null</c> if the type is not valid.</returns>
        private string ResolveTypeReference(Type type, out bool isModelType, bool isResultType = false)
        {
            isModelType = nameToDataModel.ContainsKey(type.FullName);

            if (isResultType)
            {
                if (type == typeof(void) || type == typeof(Task) || type == typeof(IActionResult))
                {
                    // These types are all essentially a way of specifying [void].

                    return "void";
                }
                else if (type.IsGenericType)
                {
                    // We need to extract the type parameter from [Task<T>] or
                    // [ActionResult<T>] as a special case.

                    var typeRef = GetTypeName(type);

                    switch (typeRef)
                    {
                        case "Task":
                        case "ActionResult":

                            type = type.GenericTypeArguments.First();
                            break;
                    }
                }
            }

            if (type == typeof(void))
            {
                // This is not a valid member or parameter type.

                return null;
            }

            if (type.IsPrimitive || !type.IsArray && !type.IsGenericType)
            {
                return GetTypeName(type);
            }

            if (type.IsArray)
            {
                // We need to handle jagged arrays where the element type 
                // is also an array.  We'll accomplish this by walking down
                // the element types until we get to a non-array element type,
                // counting how many subarrays there were.

                var arrayDepth  = 0;
                var elementType = type.GetElementType();

                while (elementType.IsArray)
                {
                    arrayDepth++;
                    elementType = elementType.GetElementType();
                }

                var arrayRef = ResolveTypeReference(elementType);

                for (int i = 0; i <= arrayDepth; i++)
                {
                    arrayRef += "[]";
                }

                return arrayRef;
            }
            else if (type.IsGenericType)
            {
                var genericRef    = GetTypeName(type);
                var genericParams = string.Empty;

                foreach (var genericParamType in type.GetGenericArguments())
                {
                    if (genericParams.Length > 0)
                    {
                        genericParams += ", ";
                    }

                    genericParams += genericParamType.Name;
                }

                return $"{genericRef}<{genericParams}>";
            }

            Covenant.Assert(false); // We should never get here.
            return null;
        }

        /// <summary>
        /// Concatenates zero or more service route templates into an absolute 
        /// route template.
        /// </summary>
        /// <param name="routes">The route templates being concatenated.</param>
        /// <returns>The absolute route template.</returns>
        private string ConcatRoutes(params string[] routes)
        {
            var routeTemplate = "/";

            foreach (var rawRoute in routes)
            {
                var route = rawRoute;

                if (string.IsNullOrEmpty(route))
                {
                    continue;
                }

                if (route.StartsWith("/"))
                {
                    route = route.Substring(1);
                }

                if (!routeTemplate.EndsWith("/"))
                {
                    routeTemplate += "/";
                }

                routeTemplate += route;
            }

            return routeTemplate;
        }
    }
}
