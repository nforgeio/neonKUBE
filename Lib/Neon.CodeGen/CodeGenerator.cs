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
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;

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
                    output.Errors.Add($"[{type.FullName}]: Enumeration base type [{enumBaseType.FullName}] is not supported.");

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
    }
}
