//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cluster;
using Neon.Cryptography;
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
        /// Enumerates the output verbosity levels.
        /// </summary>
        private enum Verbosity : int
        {
            /// <summary>
            /// Always writes output.
            /// </summary>
            Important = 0,

            /// <summary>
            /// Writes information messages.
            /// </summary>
            Info = 1,

            /// <summary>
            /// Writes trace messages.
            /// </summary>
            Trace = 2
        }

        /// <summary>
        /// Module execution state.
        /// </summary>
        private class ModuleContext
        {
            private List<string>    output = new List<string>();
            private List<string>    error  = new List<string>();

            /// <summary>
            /// The modulke name.
            /// </summary>
            [JsonIgnore]
            public string Module { get; set; }

            /// <summary>
            /// The output verbosity.
            /// </summary>
            [JsonIgnore]
            public Verbosity Verbosity { get; set; } = Verbosity.Important;

            /// <summary>
            /// The Ansible module arguments.
            /// </summary>
            [JsonIgnore]
            public JObject Arguments { get; set; }

            /// <summary>
            /// Indicates whether the model is being executed in Ansible <b>check mode</b>.
            /// </summary>
            [JsonIgnore]
            public bool CheckMode { get; set; }

            /// <summary>
            /// The cluster login.
            /// </summary>
            [JsonIgnore]
            public ClusterLogin Login { get; set; }

            /// <summary>
            /// Initializes the Ansible module arguments.
            /// </summary>
            /// <param name="argsPath">Path to the Ansible arguments file.</param>
            public void SetArguments(string argsPath)
            {
                Arguments = JObject.Parse(File.ReadAllText(argsPath));

                if (Arguments.TryGetValue<int>("_ansible_verbosity", out var ansibleVerbosity))
                {
                    this.Verbosity = (Verbosity)ansibleVerbosity;
                }
            }

            //-----------------------------------------------------------------
            // These standard output fields are described here:
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
            public List<ModuleContext> Results { get; set; } = null;

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
            /// Writes a line of text to the standard output lines.
            /// </summary>
            /// <param name="verbosity">The verbosity level for this message.</param>
            /// <param name="value">The text to be written.</param>
            public void WriteLine(Verbosity verbosity, string value = null)
            {
                if (verbosity <= this.Verbosity)
                {
                    output.Add(value ?? string.Empty);
                }
            }

            /// <summary>
            /// Writes a line of text to the standard error lines.
            /// </summary>
            /// <param name="value">The text to be written.</param>
            public void WriteErrorLine(string value = null)
            {
                error.Add(value ?? string.Empty);
            }

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
                else if (error.Count > 0)
                {
                    StdErrLines = error;
                }

                if (!string.IsNullOrEmpty(StdOut))
                {
                    StdOutLines = StdOut.ToLines().ToList();
                }
                else if (output.Count > 0)
                {
                    StdOutLines = output;
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

            var context = new ModuleContext()
            {
                Module = module
            };

            try
            {
                // Verify that we're running in the context of another Ansible
                // command (probably [exec] or [play]).

                if (Environment.GetEnvironmentVariable("IN_NEON_ANSIBLE_COMMAND") == null)
                {
                    throw new NotSupportedException("Built-in neonCLUSTER Ansible modules can run only within [neon ansible exec] or [play].");
                }

                // Read the Ansible module arguments.

                var argsPath = commandLine.Arguments.ElementAtOrDefault(1);

                if (string.IsNullOrEmpty(argsPath))
                {
                    throw new ArgumentException("Expected a path to the module arguments file.");
                }

                context.Login = login;

                context.SetArguments(argsPath);

                // Connect to the cluster so the NeonClusterHelper methods will work.

                NeonClusterHelper.OpenCluster(login);

                switch (module.ToLowerInvariant())
                {
                    case "neon_certificate":

                        RunCertificateModule(context);
                        break;

                    case "neon_dashboard":

                        RunDashboardModule(context);
                        break;

                    case "neon_dns":

                        RunDnsModule(context);
                        break;

                    case "neon_route":

                        RunRouteModule(context);
                        break;

                    default:

                        throw new ArgumentException($"[{module}] is not a recognized neonCLUSTER Ansible module.");
                }
            }
            catch (Exception e)
            {
                if (context == null)
                {
                    context = new ModuleContext();
                }

                context.Failed  = true;
                context.Message = e.Message;
            }

            Console.WriteLine(context.ToString());

            // Exit right now to be sure that nothing else is written to STDOUT.

            Program.Exit(0);
        }
    }
}
