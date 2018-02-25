//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Modules.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace NeonCli
{
    public partial class AnsibleCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Standard Ansible module outputs.
        /// </summary>
        private class ModuleOutput
        {
            // These values are describer here:
            //
            //      http://docs.ansible.com/ansible/latest/common_return_values.html
            //
            // Note that we're not currently implementing the INTERNAL properties.

            /// <summary>
            /// For those modules that implement backup=no|yes when manipulating files, a path to the backup file created.
            /// </summary>
            [JsonProperty(PropertyName = "backup_file", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(false)]
            public string BackupFile { get; set; } = null;

            /// <summary>
            /// A boolean indicating if the task had to make changes.
            /// </summary>
            [JsonProperty(PropertyName = "changed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
            [DefaultValue(false)]
            public bool Changed { get; set; } = false;

            /// <summary>
            /// A boolean that indicates if the task was failed or not.
            /// </summary>
            [JsonProperty(PropertyName = "failed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
            [DefaultValue(false)]
            public bool Failed { get; set; } = false;

            /// <summary>
            /// A string with a generic message relayed to the user.
            /// </summary>
            [JsonProperty(PropertyName = "msg", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue("")]
            public string Message { get; set; } = string.Empty;

            /// <summary>
            /// Some modules execute command line utilities or are geared
            /// for executing commands directly (raw, shell, command, etc), 
            /// this field contains <b>return code</b>k of these utilities.
            /// </summary>
            [JsonProperty(PropertyName = "rc", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(0)]
            public int ReturnCode { get; set; } = 0;

            /// <summary>
            /// If this key exists, it indicates that a loop was present for the task 
            /// and that it contains a list of the normal module <b>result</b> per item.
            /// </summary>
            [JsonProperty(PropertyName = "results", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(null)]
            public List<ModuleOutput> Results { get; set; } = null;

            /// <summary>
            /// A boolean that indicates if the task was skipped or not.
            /// </summary>
            [JsonProperty(PropertyName = "skipped", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
            [DefaultValue(false)]
            public bool Skipped { get; set; } = false;

            /// <summary>
            /// Some modules execute command line utilities or are geared for executing 
            /// commands  directly (raw, shell, command, etc), this field contains the 
            /// error output of these utilities.
            /// </summary>
            [JsonProperty(PropertyName = "stderr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue("")]
            public string StdErr { get; set; } = string.Empty;

            /// <summary>
            /// When stdout is returned, Ansible always provides a list of strings, each
            /// containing one item per line from the original output.
            /// </summary>
            [JsonProperty(PropertyName = "stderr_lines", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(null)]
            public List<string> StdErrLines { get; set; } = null;

            /// <summary>
            /// Some modules execute command line utilities or are geared for executing 
            /// commands directly (raw, shell, command, etc). This field contains the
            /// normal output of these utilities.
            /// </summary>
            [JsonProperty(PropertyName = "stdout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue("")]
            public string StdOut { get; set; } = string.Empty;

            /// <summary>
            /// When stdout is returned, Ansible always provides a list of strings, each
            /// containing one item per line from the original output.
            /// </summary>
            [JsonProperty(PropertyName = "stdout_lines", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(null)]
            public List<string> StdOutLines { get; set; } = null;

            /// <summary>
            /// Renders the instance as a JSON string.
            /// </summary>
            public override string ToString()
            {
                // Set [StdErrLines] and [StdOutLines] if necessary.

                if (!string.IsNullOrEmpty(StdErr))
                {
                    StdErrLines = StdErr.ToLines().ToList();
                }

                if (!string.IsNullOrEmpty(StdOut))
                {
                    StdOutLines = StdOut.ToLines().ToList();
                }

                return NeonHelper.JsonSerialize(this, Formatting.Indented);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Executes a built-in neonCLUSTER Ansible module. 
        /// </summary>
        /// <param name="login">The cluster login.</param>
        /// <param name="commandLine">The module command line: MODULE ARGS...</param>
        private void ExecuteModule(ClusterLogin login, CommandLine commandLine)
        {
            var module = commandLine.Arguments.ElementAtOrDefault(0);

            if (commandLine.HasHelpOption || module == null)
            {
                Console.WriteLine(moduleHelp);
                Program.Exit(0);
            }

            GenerateAnsibleConfig();
            GenerateAnsibleFiles(login);

            var output = new ModuleOutput();

            try
            {
                var argsPath = commandLine.Arguments.ElementAtOrDefault(1);

                if (string.IsNullOrEmpty(argsPath))
                {
                    throw new ArgumentException("Expected a path to the module arguments file.");
                }

                var args = File.ReadAllText(argsPath);

                switch (module.ToLowerInvariant())
                {
                    case "neon_certificate":

                        output = ImplementCertificateModule(args);
                        break;

                    case "neon_route":

                        output = ImplementRouteModule(args);
                        break;

                    default:

                        throw new ArgumentException($"[{module}] is not a recognized neonCLUSTER Ansible module.");
                }
            }
            catch (Exception e)
            {
                if (output == null)
                {
                    output = new ModuleOutput();
                }

                output.Failed  = true;
                output.Message = e.Message;
            }

            Console.WriteLine(output.ToString());

            // Exit right now to be sure that nothing else is written to STDOUT.

            Program.Exit(0);
        }

        /// <summary>
        /// Implements the built-in <b>neon_certificate</b> module.
        /// </summary>
        /// <param name="args">The module argument text from Ansible.</param>
        /// <returns>The <see cref="ModuleOutput"/>.</returns>
        private ModuleOutput ImplementCertificateModule(string args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Implements the built-in <b>neon_route</b> module.
        /// </summary>
        /// <param name="args">The module argument text from Ansible.</param>
        /// <returns>The <see cref="ModuleOutput"/>.</returns>
        private ModuleOutput ImplementRouteModule(string args)
        {
            throw new NotImplementedException();
        }
    }
}
