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
using System.Text;

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
            // $todo(jeff.lill): Implement this!
        }

        /// <summary>
        /// Loads the required information for a data model type.
        /// </summary>
        /// <param name="type">YThe source type.</param>
        private void LoadDataModel(Type type)
        {
            var dataModel = new DataModel(type);

            foreach (var targetAttibute in type.GetCustomAttributes<TargetAttribute>())
            {
                if (!dataModel.TargetGroups.Contains(targetAttibute.Group))
                {
                    dataModel.TargetGroups.Add(targetAttibute.Group);
                }
            }

            if (!nameToDataModel.TryAdd(type.FullName, dataModel))
            {
            }
        }
    }
}
