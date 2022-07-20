//-----------------------------------------------------------------------------
// FILE:	    TailwindExtensions.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Neon.Common;
using Neon.Tailwind.HeadlessUI;

namespace Neon.Tailwind
{
    public static class TailwindExtensions
    {
        public static Task RunTailwind(
            this IApplicationBuilder applicationBuilder,
            string inputCssPath  = "./Styles/tailwind.css",
            string outputCssPath = "./wwwroot/css/tailwind.css",
            bool watch           = true)
        {
            if (applicationBuilder == null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            EnsureConfiguration(inputCssPath);

            var executable = "npx";
            var args = new List<string>();

            if (OperatingSystem.IsWindows())
            {
                executable = "cmd";
                args.Add("/c");
                args.Add("npx");
            }

            args.Add("tailwindcss");
            args.Add("-i");
            args.Add(inputCssPath);
            args.Add("-o");
            args.Add(outputCssPath);

            if (watch)
            {
                args.Add("--watch");
            }

            return NeonHelper.ExecuteAsync(executable, args.ToArray());
        }

        public static Task RunTailwind(
            this IApplicationBuilder applicationBuilder,
            string script)
        {
            if (applicationBuilder == null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            var executable = "npm";
            var args       = new List<string>();

            if (OperatingSystem.IsWindows())
            {
                executable = "cmd";
                args.Add("/c");
                args.Add("npm");
            }

            args.Add("run");
            args.Add(script);

            return NeonHelper.ExecuteAsync(executable, args.ToArray());

        }

        public static IServiceCollection AddTailwind(
            this IServiceCollection builder)
        {
            builder.AddScoped<IPortalBinder, PortalBinder>();

            return builder;
        }

        private static void EnsureConfiguration(string inputCssPath)
        {
            var fileSystem = Assembly.GetExecutingAssembly().GetResourceFileSystem("Neon.Tailwind.Resources");

            var tailwindConfigPath = "./tailwind.config.js";
            if (!File.Exists(tailwindConfigPath))
            {
                var file = fileSystem.GetFile("/tailwindconfig.js");
                File.WriteAllText(tailwindConfigPath, file.ReadAllText());
            }

            if (!File.Exists(inputCssPath))
            {
                var file = fileSystem.GetFile("/tailwind.css");
                Directory.CreateDirectory(Path.GetDirectoryName(inputCssPath));
                File.WriteAllText(inputCssPath, file.ReadAllText());
            }
        }
    }
}
