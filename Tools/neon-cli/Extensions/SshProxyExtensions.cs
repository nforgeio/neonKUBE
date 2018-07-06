//-----------------------------------------------------------------------------
// FILE:	    SshProxyExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Hive;
using Neon.IO;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// <see cref="SshProxy{T}"/> extension methods.
    /// </summary>
    public static class SshProxyExtension
    {
        /// <summary>
        /// Converts a string into a value suitable for use in a Bash script.
        /// </summary>
        /// <param name="value">The value to be made safe,</param>
        /// <returns>The safe value.</returns>
        private static string BashSafeValue(object value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }
            else if (value is int)
            {
                return value.ToString();
            }
            else
            {
                // We need to escape single and double quotes.

                var stringValue = value.ToString();

                stringValue = stringValue.Replace("'", "\\'");
                stringValue = stringValue.Replace("\"", "\\\"");

                return $"\"{stringValue}\"";
            }
        }

        /// <summary>
        /// Sets a variable in a <see cref="PreprocessReader"/> such that the value will be safe
        /// to be included in a Bash variable set statement.
        /// </summary>
        /// <param name="preprocessReader">The reader.</param>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The variable value.</param>
        private static void SetBashVariable(PreprocessReader preprocessReader, string name, object value)
        {
            Covenant.Requires<ArgumentNullException>(preprocessReader != null);
            Covenant.Requires<ArgumentNullException>(name != null);

            if (value == null)
            {
                preprocessReader.Set(name, value);
            }
            else
            {
                if (value is bool)
                {
                    value = (bool)value ? "true" : "false";
                }
                else if (value is int)
                {
                    value = value.ToString();
                }
                else
                {
                    // We need to escape single and double quotes.

                    var stringValue = value.ToString();

                    stringValue = stringValue.Replace("'", "\\'");
                    stringValue = stringValue.Replace("\"", "\\\"");

                    value = $"\"{stringValue}\"";
                }

                preprocessReader.Set(name, value);
            }
        }

        /// <summary>
        /// Returns the IP address for a node suitable for including in the
        /// <b>/etc/hosts</b> file.  
        /// </summary>
        /// <param name="nodeDefinition">The target node definition.</param>
        /// <returns>
        /// The IP address, left adjusted with necessary spaces so that the
        /// host definitions will align nicely.
        /// </returns>
        private static string GetHostsFormattedAddress(NodeDefinition nodeDefinition)
        {
            const string ip4Max = "255.255.255.255";

            var address = nodeDefinition.PrivateAddress.ToString();

            if (address.Length < ip4Max.Length)
            {
                address += new string(' ', ip4Max.Length - address.Length);
            }

            return address;
        }

        /// <summary>
        /// Generates the PowerDNS Recursor hosts file for a node.  This will be uploaded
        /// to <b>/etc/powerdns/hosts</b>.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <param name="nodeDefinition">The target node definition.</param>
        /// <returns>The host definitions.</returns>
        private static string GetPowerDnsHosts(HiveDefinition hiveDefinition, NodeDefinition nodeDefinition)
        {
            var sbHosts = new StringBuilder();

            sbHosts.AppendLineLinux("# PowerDNS Recursor authoritatively answers for [*.hive] hostnames.");
            sbHosts.AppendLineLinux("# on the local node using these mappings.");
            sbHosts.AppendLineLinux();

            sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(nodeDefinition)} {HiveHostNames.Consul}");

            sbHosts.AppendLineLinux();
            sbHosts.AppendLineLinux("# Internal hive Vault mappings:");
            sbHosts.AppendLineLinux();
            sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(nodeDefinition)} {HiveHostNames.Vault}");

            foreach (var manager in hiveDefinition.Managers)
            {
                sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(manager)} {manager.Name}.{HiveHostNames.Vault}");
            }

            if (hiveDefinition.Docker.RegistryCache)
            {
                sbHosts.AppendLineLinux();
                sbHosts.AppendLineLinux("# Internal hive registry cache related mappings:");
                sbHosts.AppendLineLinux();

                foreach (var manager in hiveDefinition.Managers)
                {
                    sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(manager)} {manager.Name}.{HiveHostNames.RegistryCache}");
                }
            }

            if (hiveDefinition.Log.Enabled)
            {
                sbHosts.AppendLineLinux();
                sbHosts.AppendLineLinux("# Internal hive log pipeline related mappings:");
                sbHosts.AppendLineLinux();

                sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(nodeDefinition)} {HiveHostNames.LogEsData}");
            }

            return sbHosts.ToString();
        }

        /// <summary>
        /// Sets hive definition related variables for a <see cref="PreprocessReader"/>.
        /// </summary>
        /// <param name="preprocessReader">The reader.</param>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <param name="nodeDefinition">The target node definition.</param>
        private static void SetHiveVariables(PreprocessReader preprocessReader, HiveDefinition hiveDefinition, NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(preprocessReader != null);
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            // Generate the manager node variables in sorted order.  The variable 
            // names will be formatted as:
            //
            //      NEON_MANAGER_#
            //
            // where [#] is the zero-based index of the node.  This is compatible
            // with the [getmanager] function included the script.
            //
            // Each variable defines an associative array with [name] and [address]
            // properties.
            //
            // Then generate the NEON_MANAGER_NAMES and NEON_MANAGER_ADDRESSES arrays.
            //
            // NOTE: We need to use Linux-style line endings.

            var sbManagers                  = new StringBuilder();
            var sbManagerNamesArray         = new StringBuilder();
            var sbManagerAddressesArray     = new StringBuilder();
            var sbPeerManagerAddressesArray = new StringBuilder();
            var sbManagerNodesSummary       = new StringBuilder();
            var index                       = 0;
            var managerNameWidth            = 0;

            sbManagerNamesArray.Append("(");
            sbManagerAddressesArray.Append("(");
            sbPeerManagerAddressesArray.Append("(");

            foreach (var manager in hiveDefinition.SortedManagers)
            {
                sbManagers.Append($"declare -x -A NEON_MANAGER_{index}\n");
                sbManagers.Append($"NEON_MANAGER_{index}=( [\"name\"]=\"{manager.Name}\" [\"address\"]=\"{manager.PrivateAddress}\" )\n");
                sbManagers.Append("\n");
                index++;

                sbManagerNamesArray.Append($" \"{manager.Name}\"");
                sbManagerAddressesArray.Append($" \"{manager.PrivateAddress}\"");

                if (manager != nodeDefinition)
                {
                    sbPeerManagerAddressesArray.Append($" \"{manager.PrivateAddress}\"");
                }

                managerNameWidth = Math.Max(manager.Name.Length, managerNameWidth);
            }

            sbManagerNamesArray.Append(" )");
            sbManagerAddressesArray.Append(" )");
            sbPeerManagerAddressesArray.Append(" )");

            foreach (var manager in hiveDefinition.SortedManagers)
            {
                var nameField = manager.Name;

                if (nameField.Length < managerNameWidth)
                {
                    nameField += new string(' ', managerNameWidth - nameField.Length);
                }

                // The blanks below are just enough so that the "=" sign lines up
                // with the summary output from [hive.conf.sh].

                if (sbManagerNodesSummary.Length == 0)
                {
                    sbManagerNodesSummary.Append($"    echo \"NEON_MANAGER_NODES                 = {nameField}: {manager.PrivateAddress}\" 1>&2\n");
                }
                else
                {
                    sbManagerNodesSummary.Append($"    echo \"                                     {nameField}: {manager.PrivateAddress}\" 1>&2\n");
                }
            }

            foreach (var manager in hiveDefinition.SortedManagers)
            {
                sbManagers.Append($"declare -x -A NEON_MANAGER_{index}\n");
                sbManagers.Append($"NEON_MANAGER_{index}=( [\"name\"]=\"{manager.Name}\" [\"address\"]=\"{manager.PrivateAddress}\" )\n");
                index++;
            }

            sbManagers.Append("\n");
            sbManagers.Append($"declare -x NEON_MANAGER_NAMES={sbManagerNamesArray}\n");
            sbManagers.Append($"declare -x NEON_MANAGER_ADDRESSES={sbManagerAddressesArray}\n");

            sbManagers.Append("\n");

            if (hiveDefinition.Managers.Count() > 1)
            {
                sbManagers.Append($"declare -x NEON_MANAGER_PEERS={sbPeerManagerAddressesArray}\n");
            }
            else
            {
                sbManagers.Append("export NEON_MANAGER_PEERS=\"\"\n");
            }

            // Generate the manager and worker NTP time sources.

            var managerTimeSources = string.Empty;
            var workerTimeSources  = string.Empty;

            if (hiveDefinition.TimeSources != null)
            {
                foreach (var source in hiveDefinition.TimeSources)
                {
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        continue;
                    }

                    if (managerTimeSources.Length > 0)
                    {
                        managerTimeSources += " ";
                    }

                    managerTimeSources += $"\"{source}\"";
                }
            }

            foreach (var manager in hiveDefinition.SortedManagers)
            {
                if (workerTimeSources.Length > 0)
                {
                    workerTimeSources += " ";
                }

                workerTimeSources += $"\"{manager.PrivateAddress}\"";
            }

            if (string.IsNullOrWhiteSpace(managerTimeSources))
            {
                // Default to reasonable public time sources.

                managerTimeSources = "\"pool.ntp.org\"";
            }

            // Generate the Docker daemon command line options.  The [/etc/default/docker] script uses this.

            var sbDockerOptions = new StringBuilder();

            if (Program.ServiceManager == ServiceManager.Systemd)
            {
                sbDockerOptions.AppendWithSeparator($"-H unix:///var/run/docker.sock");

                if (hiveDefinition.Log.Enabled)
                {
                    // Metricbeat needs Docker API access.

                    sbDockerOptions.AppendWithSeparator($"-H {HiveConst.DockerApiInternalEndpoint}");
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            if (hiveDefinition.DebugMode)
            {
                // Expose the Docker Swarm REST API on the node's internal hive IP address so it
                // can be reached by apps like [neon-proxy-manager] running off the manager node
                // (potentially in the debugger).

                sbDockerOptions.AppendWithSeparator($"-H tcp://{nodeDefinition.PrivateAddress}:{NetworkPorts.Docker}");
            }

            preprocessReader.Set("docker.options", sbDockerOptions);
            
            // Define the Consul command line options.

            var consulOptions = string.Empty;

            if (hiveDefinition.Dashboard.Consul)
            {
                if (consulOptions.Length > 0)
                {
                    consulOptions += " ";
                }

                consulOptions += "-ui";
            }

            // Format the network upstream nameservers as semicolon separated
            // to be compatible with the PowerDNS Recursor [forward-zones-recurse]
            // configuration setting.
            //
            // Note that manager nodes will recurse to upstream (external) DNS 
            // servers and workers/pets will recurse to the managers so they can
            // dynamically pickup hive DNS changes.

            if (hiveDefinition.Network?.Nameservers == null)
            {
                // $hack(jeff.lill): 
                //
                // [Network] will be null if we're just preparing servers, not doing full setup
                // so we'll set this to the defaults to avoid null references below.

                hiveDefinition.Network = new NetworkOptions();
            }

            var nameservers = string.Empty;

            if (nodeDefinition.Role == NodeRole.Manager)
            {
                for (int i = 0; i < hiveDefinition.Network.Nameservers.Length; i++)
                {
                    if (i > 0)
                    {
                        nameservers += ";";
                    }

                    nameservers += hiveDefinition.Network.Nameservers[i].Trim();
                }
            }
            else
            {
                foreach (var manager in hiveDefinition.SortedManagers)
                {
                    if (nameservers.Length > 0)
                    {
                        nameservers += ";";
                    }

                    nameservers += manager.PrivateAddress;
                }
            }

            // Set the variables.

            preprocessReader.Set("load-hive-conf", HiveHostFolders.Config + "/hive.conf.sh --echo-summary");
            preprocessReader.Set("load-hive-conf-quiet", HiveHostFolders.Config + "/hive.conf.sh");

            SetBashVariable(preprocessReader, "hive.provisioner", hiveDefinition.Provisioner);
            SetBashVariable(preprocessReader, "hive.rootuser", Program.MachineUsername);

            SetBashVariable(preprocessReader, "node.driveprefix", hiveDefinition.DrivePrefix);

            SetBashVariable(preprocessReader, "neon.folders.config", HiveHostFolders.Config);
            SetBashVariable(preprocessReader, "neon.folders.secrets", HiveHostFolders.Secrets);
            SetBashVariable(preprocessReader, "neon.folders.setup", HiveHostFolders.Setup);
            SetBashVariable(preprocessReader, "neon.folders.tools", HiveHostFolders.Tools);
            SetBashVariable(preprocessReader, "neon.folders.state", HiveHostFolders.State);
            SetBashVariable(preprocessReader, "neon.folders.secrets", HiveHostFolders.Secrets);
            SetBashVariable(preprocessReader, "neon.folders.scripts", HiveHostFolders.Scripts);
            SetBashVariable(preprocessReader, "neon.folders.archive", HiveHostFolders.Archive);
            SetBashVariable(preprocessReader, "neon.folders.exec", HiveHostFolders.Exec);

            preprocessReader.Set("neon.hosts.neon-log-es-data", HiveHostNames.LogEsData);

            SetBashVariable(preprocessReader, "nodes.manager.count", hiveDefinition.Managers.Count());
            preprocessReader.Set("nodes.managers", sbManagers);
            preprocessReader.Set("nodes.manager.summary", sbManagerNodesSummary);

            SetBashVariable(preprocessReader, "ntp.manager.sources", managerTimeSources);
            SetBashVariable(preprocessReader, "ntp.worker.sources", workerTimeSources);

            if (!hiveDefinition.BareDocker)
            {
                // When we're not deploying bare Docker, the manager nodes will use the 
                // configured name servers as the hive's upstream DNS and the worker
                // nodes will be configured to query the name servers.

                if (nodeDefinition.IsManager)
                {
                    preprocessReader.Set("net.nameservers", nameservers);
                }
                else
                {
                    var managerNameservers = string.Empty;

                    foreach (var manager in hiveDefinition.Managers)
                    {
                        if (managerNameservers.Length > 0)
                        {
                            managerNameservers += ";";
                        }

                        managerNameservers += manager.PrivateAddress.ToString();
                    }

                    preprocessReader.Set("net.nameservers", managerNameservers);
                }
            }
            else
            {
                // All servers use the configured upstream nameservers when we're not
                // deploying the Dynamic DNS.

                preprocessReader.Set("net.nameservers", nameservers);
            }

            SetBashVariable(preprocessReader, "net.powerdns.recursor.package.uri", hiveDefinition.Network.PdnsRecursorPackageUri);
            preprocessReader.Set("net.powerdns.recursor.hosts", GetPowerDnsHosts(hiveDefinition, nodeDefinition));

            SetBashVariable(preprocessReader, "docker.version", hiveDefinition.Docker.PackageVersion);

            SetBashVariable(preprocessReader, "consul.version", hiveDefinition.Consul.Version);
            SetBashVariable(preprocessReader, "consul.options", consulOptions);
            SetBashVariable(preprocessReader, "consul.address", $"{HiveHostNames.Consul}:{hiveDefinition.Consul.Port}");
            SetBashVariable(preprocessReader, "consul.fulladdress", $"http://{HiveHostNames.Consul}:{hiveDefinition.Consul.Port}");
            SetBashVariable(preprocessReader, "consul.hostname", HiveHostNames.Consul);
            SetBashVariable(preprocessReader, "consul.port", hiveDefinition.Consul.Port);
            SetBashVariable(preprocessReader, "consul.tlsdisabled", true);

            SetBashVariable(preprocessReader, "vault.version", hiveDefinition.Vault.Version);

            SetBashVariable(preprocessReader, "vault.download", $"https://releases.hashicorp.com/vault/{hiveDefinition.Vault.Version}/vault_{hiveDefinition.Vault.Version}_linux_amd64.zip");
            SetBashVariable(preprocessReader, "vault.hostname", HiveHostNames.Vault);
            SetBashVariable(preprocessReader, "vault.port", hiveDefinition.Vault.Port);
            SetBashVariable(preprocessReader, "vault.consulpath", "vault/");
            SetBashVariable(preprocessReader, "vault.maximumlease", hiveDefinition.Vault.MaximimLease);
            SetBashVariable(preprocessReader, "vault.defaultlease", hiveDefinition.Vault.DefaultLease);

            SetBashVariable(preprocessReader, "log.enabled", hiveDefinition.Log.Enabled);
        }

        /// <summary>
        /// Uploads a resource file to the remote server after performing any necessary preprocessing.
        /// </summary>
        /// <typeparam name="TMetadata">The node metadata type.</typeparam>
        /// <param name="node">The remote node.</param>
        /// <param name="hiveDefinition">The hive definition or <c>null</c>.</param>
        /// <param name="file">The resource file.</param>
        /// <param name="targetPath">The target path on the remote server.</param>
        private static void UploadFile<TMetadata>(this SshProxy<TMetadata> node, HiveDefinition hiveDefinition, ResourceFiles.File file, string targetPath)
            where TMetadata : class
        {
            using (var input = file.ToStream())
            {
                if (file.HasVariables)
                {
                    // We need to expand any variables.  Note that if we don't have a
                    // hive definition or for undefined variables, we're going to 
                    // have the variables expand to the empty string.

                    using (var msExpanded = new MemoryStream())
                    {
                        using (var writer = new StreamWriter(msExpanded))
                        {
                            var preprocessReader =
                                new PreprocessReader(new StreamReader(input))
                                {
                                    DefaultVariable = string.Empty,
                                    ExpandVariables = true,
                                    ProcessCommands = false,
                                    StripComments   = false
                                };

                            if (hiveDefinition != null)
                            {
                                SetHiveVariables(preprocessReader, hiveDefinition, node.Metadata as NodeDefinition);
                            }

                            foreach (var line in preprocessReader.Lines())
                            {
                                writer.WriteLine(line);
                            }

                            writer.Flush();

                            msExpanded.Position = 0;
                            node.UploadText(targetPath, msExpanded, tabStop: 4, outputEncoding: Encoding.UTF8);
                        }
                    }
                }
                else
                {
                    node.UploadText(targetPath, input, tabStop: 4, outputEncoding: Encoding.UTF8);
                }
            }
        }

        /// <summary>
        /// Uploads the configuration files for the target operating system to the server.
        /// </summary>
        /// <typeparam name="Metadata">The node metadata type.</typeparam>
        /// <param name="node">The remote node.</param>
        /// <param name="hiveDefinition">The hive definition or <c>null</c>.</param>
        public static void UploadConfigFiles<Metadata>(this SshProxy<Metadata> node, HiveDefinition hiveDefinition = null)
            where Metadata : class
        {
            Covenant.Requires<ArgumentNullException>(node != null);

            // Clear the contents of the configuration folder.

            node.Status = $"clear: {HiveHostFolders.Config}";
            node.SudoCommand($"rm -rf {HiveHostFolders.Config}/*.*");

            // Upload the files.

            node.Status = "upload: config files";

            foreach (var file in Program.LinuxFolder.GetFolder("conf").Files())
            {
                node.UploadFile(hiveDefinition, file, $"{HiveHostFolders.Config}/{file.Name}");
            }

            // Secure the files and make the scripts executable.

            node.SudoCommand($"chmod 600 {HiveHostFolders.Config}/*.*");
            node.SudoCommand($"chmod 700 {HiveHostFolders.Config}/*.sh");

            node.Status = "copied";
        }

        /// <summary>
        /// Uploads the setup and other scripts and tools for the target operating system to the server.
        /// </summary>
        /// <typeparam name="TMetadata">The server's metadata type.</typeparam>
        /// <param name="server">The remote server.</param>
        /// <param name="hiveDefinition">The hive definition or <c>null</c>.</param>
        public static void UploadTools<TMetadata>(this SshProxy<TMetadata> server, HiveDefinition hiveDefinition = null)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(server != null);

            //-----------------------------------------------------------------
            // Clear the contents of the setup scripts folder.

            server.Status = $"clear: {HiveHostFolders.Setup}";
            server.SudoCommand($"rm -rf {HiveHostFolders.Setup}/*.*");

            // Upload the setup files.

            server.Status = "upload: setup files";

            foreach (var file in Program.LinuxFolder.GetFolder("setup").Files())
            {
                server.UploadFile(hiveDefinition, file, $"{HiveHostFolders.Setup}/{file.Name}");
            }

            // Make the scripts executable.

            server.SudoCommand($"chmod 700 {HiveHostFolders.Setup}/*");

            //-----------------------------------------------------------------
            // Clear the contents of the tools folder.

            server.Status = $"clear: {HiveHostFolders.Tools}";
            server.SudoCommand($"rm -rf {HiveHostFolders.Tools}/*.*");

            // Upload the tool files.  Note that we're going to strip out the [.sh] 
            // file type to make these easier to run.

            server.Status = "upload: tool files";

            foreach (var file in Program.LinuxFolder.GetFolder("tools").Files())
            {
                server.UploadFile(hiveDefinition, file, $"{HiveHostFolders.Tools}/{file.Name.Replace(".sh", string.Empty)}");
            }

            // Make the scripts executable.

            server.SudoCommand($"chmod 700 {HiveHostFolders.Tools}/*");
        }
    }
}
