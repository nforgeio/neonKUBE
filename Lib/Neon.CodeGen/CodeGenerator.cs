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
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Newtonsoft.Json;

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
        //---------------------------------------------------------------------
        // Static members

        private static MetadataReference cachedNetStandard;

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
            CSharpCompilationOptions        options = null)
        {
            Covenant.Requires<ArgumentNullException>(source != null);

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new MetadataReferences();

            // Allow the caller to add references.

            referenceHandler?.Invoke(references);

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

        private CodeGeneratorOutput                 output;
        private Dictionary<string, DataModel>       nameToDataModel    = new Dictionary<string, DataModel>();
        private Dictionary<string, ServiceModel>    nameToServiceModel = new Dictionary<string, ServiceModel>();
        private StringWriter                        writer;
        private string                              targetGroup;
        private string                              targetNamespace;
        private string                              sourceNamespace;

        /// <summary>
        /// Constructs a code generator.
        /// </summary>
        /// <param name="settings">Optional settings.  Reasonable defaults will be used when this is <c>null</c>.</param>
        public CodeGenerator(CodeGeneratorSettings settings = null)
        {
            this.Settings = settings ?? new CodeGeneratorSettings();

            this.sourceNamespace = settings.SourceNamespace;

            if (string.IsNullOrEmpty(sourceNamespace))
            {
                this.sourceNamespace = null;
            }
            else
            {
                if (!sourceNamespace.EndsWith("."))
                {
                    sourceNamespace += ".";
                }
            }
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
        public CodeGeneratorOutput Generate(params Assembly[] assemblies)
        {
            Covenant.Requires<ArgumentNullException>(assemblies != null);
            Covenant.Requires<ArgumentException>(assemblies.Length > 0, "At least one assembly must be passed.");

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
                if (sourceNamespace != null && !type.FullName.StartsWith(sourceNamespace))
                {
                    // Ignore any types that aren't in specified source namespace.

                    continue;
                }

                if (type.GetCustomAttribute<NoCodeGenAttribute>() != null)
                {
                    // Ignore any types tagged with [NoCodeGen].

                    continue;
                }

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
            if (Settings.TargetGroups.Count == 0)
            {
                // Treat an empty list as enabling all groups.

                return;
            }

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
                    output.Errors.Add($"*** ERROR: [{type.FullName}]: Enumeration base type [{enumBaseType.FullName}] is not supported.");

                    dataModel.BaseTypeName = "int";
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
                // A data model interface is allowed to implement another 
                // data model interface to specify a base class.  Note that
                // only one of these references is allowed and it may only
                // be a reference to another data model (not an arbetrary 
                // type.

                var baseInterface = (Type)null;

                foreach (var implementedInterface in type.GetInterfaces())
                {
                    if (!nameToDataModel.ContainsKey(implementedInterface.FullName))
                    {
                        output.Errors.Add($"*** ERROR: [{dataModel.SourceType.FullName}]: This data model inherits [{implementedInterface.FullName}] which is not defined in a source assembly.");
                    }

                    if (baseInterface != null)
                    {
                        output.Errors.Add($"*** ERROR: [{dataModel.SourceType.FullName}]: This data model inherits from multiple base types.  A maximum of one is allowed.");
                    }

                    baseInterface = implementedInterface;
                }

                dataModel.BaseTypeName = baseInterface?.FullName;

                // Normalize regular (non-enum) data model properties.

                foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Ignore properties that don't have both a getter and a setter.

                    if (member.GetAccessors().Length != 2)
                    {
                        continue;
                    }

                    var property = new DataProperty()
                    {
                        Name = member.Name,
                        Type = member.PropertyType
                    };

                    property.Ignore = member.GetCustomAttribute<JsonIgnoreAttribute>() != null;

                    var jsonPropertyAttribute = member.GetCustomAttribute<JsonPropertyAttribute>();

                    if (jsonPropertyAttribute != null)
                    {
                        property.SerializedName       = jsonPropertyAttribute.PropertyName;
                        property.Order                = jsonPropertyAttribute.Order;
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
                        output.Errors.Add($"*** ERROR: [{dataModel.SourceType.FullName}]: This data model references type [{propertyType.FullName}] which is not defined in a source assembly.");
                    }
                }
            }

            // Ensure that all service method parameter and result types are either
            // a primitive .NET type, a type implemented within [mscorlib] or a
            // reference a loaded data model.

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
            writer.WriteLine($"#pragma warning disable 0108     // Disable property overrides without new warnings");
            writer.WriteLine($"#pragma warning disable 0168     // Disable declared but never used warnings");
            writer.WriteLine($"#pragma warning disable 1591     // Disable missing comment warnings");
            writer.WriteLine();
            writer.WriteLine($"using System;");
            writer.WriteLine($"using System.Collections.Generic;");
            writer.WriteLine($"using System.ComponentModel;");
            writer.WriteLine($"using System.Dynamic;");
            writer.WriteLine($"using System.IO;");
            writer.WriteLine($"using System.Runtime.Serialization;");
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
            // Generate a static class to hold the shared JSON serializer stuff.

            writer.WriteLine($"    internal static class __Json");
            writer.WriteLine($"    {{");
            writer.WriteLine($"        private static readonly JsonSerializerSettings settings;");
            writer.WriteLine();
            writer.WriteLine($"        static __Json()");
            writer.WriteLine($"        {{");
            writer.WriteLine($"            settings = new JsonSerializerSettings()");
            writer.WriteLine($"            {{");
            writer.WriteLine($"                MissingMemberHandling = MissingMemberHandling.Ignore,");
            writer.WriteLine($"                DateFormatHandling    = DateFormatHandling.IsoDateFormat,");
            writer.WriteLine($"                DateTimeZoneHandling  = DateTimeZoneHandling.Utc");
            writer.WriteLine($"            }};");
            writer.WriteLine();
            writer.WriteLine($"            settings.Converters.Add(new StringEnumConverter(new DefaultNamingStrategy(), allowIntegerValues: false));");
            writer.WriteLine($"        }}");
            writer.WriteLine();
            writer.WriteLine($"        public static string Serialize(object value, Formatting format = Formatting.None)");
            writer.WriteLine($"        {{");
            writer.WriteLine($"            return JsonConvert.SerializeObject(value, format, settings);");
            writer.WriteLine($"        }}");
            writer.WriteLine();
            writer.WriteLine($"        public static T Deserialize<T>(string jsonText)");
            writer.WriteLine($"        {{");
            writer.WriteLine($"            return JsonConvert.DeserializeObject<T>(jsonText, settings);");
            writer.WriteLine($"        }}");
            writer.WriteLine($"    }}");
            writer.WriteLine();

            //---------------------------------------------
            // Generate the models.

            var index = 0;

            foreach (var dataModel in nameToDataModel.Values
                .OrderBy(dm => dm.SourceType.Name.ToLowerInvariant()))
            {
                GenerateDataModel(dataModel, index++);
            }

            // Generate the service clients (if enabled).

            if (Settings.ServiceClients)
            {
                // $todo(jeff.lill): Implement this!
            }

            // Close the namespace.

            writer.WriteLine($"}}");

            // Set the generated source code for the code generator output.

            output.SourceCode = writer.ToString();
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
        /// <param name="index">Zero based index of the model within the current namespace.</param>
        private void GenerateDataModel(DataModel dataModel, int index)
        {
            if (index > 0)
            {
                // Add a blank line between type definitions within the namespace.

                writer.WriteLine();
            }

            writer.WriteLine($"    // From: {dataModel.SourceType.FullName}");

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
                var baseTypeRef = string.Empty;

                if (dataModel.BaseTypeName != null)
                {
                    if (!nameToDataModel.ContainsKey(dataModel.BaseTypeName))
                    {
                        output.Errors.Add($"*** ERROR: [{dataModel.SourceType.FullName}]: This data model inherits type [{dataModel.BaseTypeName}] which is not defined in a source assembly.");
                        return;
                    }

                    baseTypeRef = $" : {StripNamespace(dataModel.BaseTypeName)}";
                }

                writer.WriteLine($"    public partial class {dataModel.SourceType.Name}{baseTypeRef}");
                writer.WriteLine($"    {{");

                if (Settings.RoundTrip)
                {
                    //-------------------------------------
                    // Generate the static members.

                    writer.WriteLine($"        public static {dataModel.SourceType.Name} From(string jsonText)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (string.IsNullOrEmpty(jsonText))");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                throw new ArgumentNullException(nameof(jsonText));");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            var model = new {dataModel.SourceType.Name}(__Json.Deserialize<JObject>(jsonText));");
                    writer.WriteLine();
                    writer.WriteLine($"            model.__Load();");
                    writer.WriteLine($"            return model;");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        public static {dataModel.SourceType.Name} From(JObject jObject)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (jObject == null)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                throw new ArgumentNullException(nameof(jObject));");
                    writer.WriteLine($"            }}");
                    writer.WriteLine();
                    writer.WriteLine($"            var model = new {dataModel.SourceType.Name}(jObject);");
                    writer.WriteLine();
                    writer.WriteLine($"            model.__Load();");
                    writer.WriteLine($"            return model;");
                    writer.WriteLine($"        }}");

                    //-------------------------------------
                    // Generate instance members

                    // Generate the backing __JObject property.

                    if (dataModel.BaseTypeName == null)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        protected JObject __JObject {{ get; set; }}");
                    }
                }

                // Generate the constructors.

                writer.WriteLine();
                writer.WriteLine($"        public {dataModel.SourceType.Name}()");
                writer.WriteLine($"        {{");
                writer.WriteLine($"            __JObject = new JObject();");
                writer.WriteLine($"        }}");

                writer.WriteLine();
                writer.WriteLine($"        private {dataModel.SourceType.Name}(JObject jObject)");
                writer.WriteLine($"        {{");
                writer.WriteLine($"            __JObject = jObject;");
                writer.WriteLine($"        }}");

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

                                output.Errors.Add($"*** ERROR: [{dataModel.SourceType.FullName}]: Service model [{property.Name}] specifies an unsupported [{nameof(DefaultValueHandling)}] value.");
                                defaultValueHandling = "Include";
                                break;
                        }

                        writer.WriteLine($"        [JsonProperty(PropertyName = \"{property.SerializedName}\", DefaultValueHandling = DefaultValueHandling.{defaultValueHandling}, Order = {property.Order})]");

                        var defaultValue = GetPropertyDefaultValue(property, out var isTypeDefault);

                        if (!isTypeDefault)
                        {
                            writer.WriteLine($"        [DefaultValue({GetPropertyDefaultValue(property)})]");
                        }
                    }

                    var propertyTypeName = ResolveTypeReference(property.Type);

                    writer.WriteLine($"        public {propertyTypeName} {property.Name} {{ get; set; }}");
                }

                if (Settings.RoundTrip)
                {
                    //-------------------------------------
                    // Generate the __Load() method.

                    var virtualModifier = dataModel.BaseTypeName == null ? "virtual" : "override";

                    writer.WriteLine();
                    writer.WriteLine($"        protected {virtualModifier} void __Load()");
                    writer.WriteLine($"        {{");

                    if (dataModel.Properties.Count > 0 || dataModel.BaseTypeName != null)
                    {
                        writer.WriteLine($"            JProperty property;");
                        writer.WriteLine();
                        writer.WriteLine($"            lock (__JObject)");
                        writer.WriteLine($"            {{");

                        if (dataModel.BaseTypeName != null)
                        {
                            writer.WriteLine($"                base.__Load();");
                        }

                        var propertyIndex = 0;

                        foreach (var property in dataModel.Properties)
                        {
                            if (propertyIndex++ > 0)
                            {
                                writer.WriteLine();
                            }

                            writer.WriteLine($"                property = this.__JObject.Property(\"{property.SerializedName}\");");
                            writer.WriteLine($"                if (property != null)");
                            writer.WriteLine($"                {{");
                            writer.WriteLine($"                    this.{property.Name} = property.ToObject<{ResolveTypeReference(property.Type)}>();");
                            writer.WriteLine($"                }}");

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

                                    var defaultValue = GetPropertyDefaultValue(property, out var isTypeDefault);

                                    if (!isTypeDefault)
                                    {
                                        writer.WriteLine($"                else");
                                        writer.WriteLine($"                {{");
                                        writer.WriteLine($"                    this.{property.Name} = {defaultValue};");
                                        writer.WriteLine($"                }}");
                                    }
                                    break;
                            }
                        }

                        writer.WriteLine($"            }}");
                    }

                    writer.WriteLine($"        }}");

                    //-------------------------------------
                    // Generate the __Save() method.

                    writer.WriteLine();
                    writer.WriteLine($"        protected {virtualModifier} void __Save()");
                    writer.WriteLine($"        {{");

                    if (dataModel.Properties.Count > 0 || dataModel.BaseTypeName != null)
                    {
                        writer.WriteLine($"            JProperty property;");
                        writer.WriteLine();
                        writer.WriteLine($"            lock (__JObject)");
                        writer.WriteLine($"            {{");

                        if (dataModel.BaseTypeName != null)
                        {
                            writer.WriteLine($"                base.__Save();");

                            if (dataModel.Properties.Count > 0)
                            {
                                writer.WriteLine();
                            }
                        }

                        var propertyIndex = 0;

                        foreach (var property in dataModel.Properties)
                        {
                            if (propertyIndex++ > 0)
                            {
                                writer.WriteLine();
                            }

                            writer.WriteLine($"                property = this.__JObject.Property(\"{property.SerializedName}\");");

                            switch (property.DefaultValueHandling)
                            {
                                case DefaultValueHandling.Include:

                                    writer.WriteLine($"                this.__JObject[\"{property.SerializedName}\"] = property.Name;");
                                    break;

                                case DefaultValueHandling.Populate:
                                case DefaultValueHandling.Ignore:
                                case DefaultValueHandling.IgnoreAndPopulate:

                                    var defaultValue = GetPropertyDefaultValue(property, out var isTypeDefault);

                                    writer.WriteLine($"                if (property.Value.ToObject<{ResolveTypeReference(property.Type)}>() == {defaultValue})");
                                    writer.WriteLine($"                {{");
                                    writer.WriteLine($"                    if (property != null)");
                                    writer.WriteLine($"                    {{");
                                    writer.WriteLine($"                        this.__JObject.Remove(\"{property.SerializedName}\");");
                                    writer.WriteLine($"                    }}");
                                    writer.WriteLine($"                }}");
                                    writer.WriteLine($"                else");
                                    writer.WriteLine($"                {{");
                                    writer.WriteLine($"                    this.__JObject[\"{property.SerializedName}\"] = property.Name;");
                                    writer.WriteLine($"                }}");

                                    break;
                            }
                        }
                        writer.WriteLine($"            }}");
                    }

                    writer.WriteLine($"        }}");

                    //-------------------------------------
                    // Generate the ToString() methods.

                    // NOTE: I'm not locking __JObject here because anybody who is
                    //       trying to serialize an object that they're modifying
                    //       on another thread should hang their head in shame.

                    writer.WriteLine();
                    writer.WriteLine($"        public override string ToString()");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            __Save();");
                    writer.WriteLine($"            return __Json.Serialize(__JObject, Formatting.None);");
                    writer.WriteLine($"        }}");

                    writer.WriteLine();
                    writer.WriteLine($"        public string ToString(bool indented)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            __Save();");
                    writer.WriteLine($"            return __Json.Serialize(__JObject, indented ? Formatting.Indented : Formatting.None);");
                    writer.WriteLine($"        }}");

                    // Close the generated model class definition.

                    writer.WriteLine($"    }}");
                }
            }
        }

        /// <summary>
        /// Returns the name will use for a type when generating type references.
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
        /// <returns>The type reference as a string or <c>null</c> if the type is now valid.</returns>
        private string ResolveTypeReference(Type type)
        {
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

                for (int i = 0; i < arrayDepth; i++)
                {
                    arrayRef += "[]";
                }

                return arrayRef;
            }
            else if (type.IsGenericType)
            {
                // Generic type names look like: "System.Collections.List`1"
                // We'll strip off the part including the backtick.

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
        /// Returns the default value for a property.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>The value expression.</returns>
        private string GetPropertyDefaultValue(DataProperty property)
        {
            return GetPropertyDefaultValue(property, out var ignored);
        }

        /// <summary>
        /// Returns the default value for a property.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="isTypeDefault">Returns as <c>true</c> when the value is the type's default value.</param>
        /// <returns>The value expression.</returns>
        private string GetPropertyDefaultValue(DataProperty property, out bool isTypeDefault)
        {
            var typeDefault = $"default({ResolveTypeReference(property.Type)})";

            if (property.DefaultValue != null)
            {
                var valueExpression = property.DefaultValue.ToString();

                isTypeDefault = valueExpression == typeDefault;
                return valueExpression;
            }

            isTypeDefault = true;
            return typeDefault;
        }
    }
}
