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
        /// Standard Ansible module outputs.
        /// </summary>
        private class ModuleOutput
        {
            private List<string>    output = new List<string>();
            private List<string>    error  = new List<string>();

            /// <summary>
            /// The output verbosity.
            /// </summary>
            [JsonIgnore]
            public Verbosity Verbosity { get; set; } = Verbosity.Important;

            // These values are described here:
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
            var module   = commandLine.Arguments.ElementAtOrDefault(0);
            var isAction = commandLine.HasOption("--action");

            if (commandLine.HasHelpOption || module == null)
            {
                Console.WriteLine(moduleHelp);
                Program.Exit(0);
            }

            var output = new ModuleOutput();

            try
            {
                // Verify that we're running in the context of another Ansible
                // command (probably [exec] or [play]).

                if (Environment.GetEnvironmentVariable("IN_NEON_ANSIBLE_COMMAND") == null)
                {
                    throw new NotSupportedException("Built-in neonCLUSTER Ansible modules can run only within [neon ansible exec] or [play].");
                }

                // Identify the target module.

                var argsPath = commandLine.Arguments.ElementAtOrDefault(1);

                if (string.IsNullOrEmpty(argsPath))
                {
                    throw new ArgumentException("Expected a path to the module arguments file.");
                }

                // Connect to the cluster so the NeonClusterHelper methods will work.

                NeonClusterHelper.OpenCluster(login);

                //-------------------------------
                // $todo(jeff.lill): DELETE THIS!

                if (File.Exists("_args.json"))
                {
                    File.Delete("_args.json");
                }

                File.Copy(argsPath, Path.Combine(Environment.CurrentDirectory, "_args.json"));

                //-------------------------------

                var args = JObject.Parse(File.ReadAllText(argsPath));

                if (args.TryGetValue<int>("_ansible_verbosity", out var ansibleVerbosity))
                {
                    output.Verbosity = (Verbosity)ansibleVerbosity;
                }

                switch (module.ToLowerInvariant())
                {
                    case "neon_certificate":

                        ImplementCertificateModule(output, login, isAction, args);
                        break;

                    case "neon_route":

                        ImplementRouteModule(output, login, isAction, args);
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
        /// Converts an Ansible argument value into a boolean.
        /// </summary>
        /// <param name="value">The value being converted.</param>
        /// <returns>The converted boolean.</returns>
        private bool ToBool(string value)
        {
            // $todo(jeff.lill):
            //
            // Scan the Ansible source code and use the same conventions here
            // (if we're not doing so already).

            switch (value.ToLowerInvariant())
            {
                case "yes":
                case "true:":
                case "on":
                case "1":

                    return true;

                default:

                    return false;
            }
        }

        /// <summary>
        /// Update the <b>neon-proxy-manager</b> Consul key to indicate that changes
        /// have been made to the cluster certificates.
        /// </summary>
        private void TouchCertChanged()
        {
            using (var consul = NeonClusterHelper.OpenConsul())
            {
                consul.KV.PutString("neon/service/neon-proxy-manager/conf/cert-update", DateTime.UtcNow).Wait();
            }
        }

        //---------------------------------------------------------------------
        // neon_certificate:
        //
        // Synopsis:
        // ---------
        //
        // Creates or removes neonCLUSTER TLS certificates.
        //
        // Requirements:
        // -------------
        //
        // This module runs only within the [neon-cli] container when invoked
        // by [neon ansible exec ...] or [neon ansible play ...].
        //
        // Options:
        // --------
        //
        // parameter    required    default     choices     comments
        // --------------------------------------------------------------------
        //
        // name         yes                                 neonCLUSTER certificate name
        //
        // value        see comment                         public certificate, any intermediate
        //                                                  certificates and the private key in PEM 
        //                                                  format.  Required when [state=present]
        //
        // state        no          present     absent      indicates whether the certificate should
        //                                      present     be created or removed
        //
        // force        no          false                   resaves the certificate when state=present
        //                                                  even if the certificate is the same

        /// <summary>
        /// Implements the built-in <b>neon_certificate</b> module.
        /// </summary>
        /// <param name="output">The module outputm object.</param>
        /// <param name="login">The cluster login.</param>
        /// <param name="isAction">Indicates that the module is being executed as an <b>action plugin</b>.</param>
        /// <param name="args">The module arguments dictionary.</param>
        private void ImplementCertificateModule(ModuleOutput output, ClusterLogin login, bool isAction, JObject args)
        {
            //-------------------------------
            // Get the common arguments.

            if (!args.TryGetValue<int>("_ansible_verbosity", out var verbosity) || verbosity < 0)
            {
                verbosity = 0;
            }

            if (!args.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!ClusterDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[name={name}] is not a valid certificate name.");
            }

            if (!args.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            if (!args.TryGetValue<string>("force", out var forceArg))
            {
                forceArg = "false";
            }

            var force = ToBool(forceArg);

            // We have the required arguments, so perform the operation.

            if (login.VaultCredentials == null || string.IsNullOrEmpty(login.VaultCredentials.RootToken))
            {
                throw new ArgumentException("Access Denied: Vault root credentials are required.");
            }

            var vaultPath = NeonClusterHelper.GetVaultCertificateKey(name);

            output.WriteLine(Verbosity.Trace, $"Vault: Certificate path is [{vaultPath}]");
            output.WriteLine(Verbosity.Trace, $"Vault: Opening Vault");

            using (var vault = NeonClusterHelper.OpenVault(login.VaultCredentials.RootToken))
            {
                switch (state)
                {
                    case "absent":

                        output.WriteLine(Verbosity.Trace, $"Vault: checking for [{name}] certificate");

                        if (vault.ExistsAsync(vaultPath).Result)
                        {
                            output.WriteLine(Verbosity.Trace, $"Vault: [{name}] certificate exists");
                            output.WriteLine(Verbosity.Trace, $"Vault: Deleting [{name}]");

                            vault.DeleteAsync(vaultPath).Wait();
                            output.WriteLine(Verbosity.Info, $"Vault: [{name}] certificate deleted");

                            TouchCertChanged();
                            output.WriteLine(Verbosity.Trace, $"Consul: Signal the certificate change");

                            output.Changed = true;
                        }
                        else
                        {
                            output.WriteLine(Verbosity.Info, $"Vault: [{name}] certificate does not exist");
                        }
                        break;

                    case "present":

                        if (!args.TryGetValue<string>("value", out var value))
                        {
                            throw new ArgumentException($"[value] module argument is required.");
                        }

                        var certificate = new TlsCertificate(value);    // This validates the certificate/private key

                        certificate.Parse();

                        output.WriteLine(Verbosity.Trace, $"Vault: Reading [{name}]");

                        var existingCert = vault.ReadJsonAsync<TlsCertificate>(vaultPath, noException: true).Result;

                        if (existingCert == null)
                        {
                            output.WriteLine(Verbosity.Info, $"Vault: [{name}] certificate does not exist");
                            output.Changed = true;
                        }
                        else if (!NeonHelper.JsonEquals(existingCert, certificate))
                        {
                            output.WriteLine(Verbosity.Info, $"Vault: [{name}] certificate does exists but is different");
                            output.Changed = true;
                        }
                        else
                        {
                            output.WriteLine(Verbosity.Info, $"Vault: [{name}] certificate is unchanged");
                        }

                        if (output.Changed)
                        {
                            output.WriteLine(Verbosity.Trace, $"Vault: Saving [{name}] certificate");
                            vault.WriteJsonAsync(vaultPath, certificate).Wait();
                            output.WriteLine(Verbosity.Info, $"Vault: [{name}] certificate saved");
                        }

                        break;

                    default:

                        throw new ArgumentException($"[state={state}] is not a valid choice.");
                }
            }
        }

        /// <summary>
        /// Implements the built-in <b>neon_route</b> module.
        /// </summary>
        /// <param name="output">The module outputm object.</param>
        /// <param name="login">The cluster login.</param>
        /// <param name="isAction">Indicates that the module is being executed as an <b>action plugin</b>.</param>
        /// <param name="args">The module arguments as JSON.</param>
        private void ImplementRouteModule(ModuleOutput output, ClusterLogin login, bool isAction, JObject args)
        {
            throw new NotImplementedException();
        }
    }
}
