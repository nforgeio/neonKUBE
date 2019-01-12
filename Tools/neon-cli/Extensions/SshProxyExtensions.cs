//-----------------------------------------------------------------------------
// FILE:	    SshProxyExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Kube;
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
        /// Sets cluster definition related variables for a <see cref="PreprocessReader"/>.
        /// </summary>
        /// <param name="preprocessReader">The reader.</param>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeDefinition">The target node definition.</param>
        private static void SetHiveVariables(PreprocessReader preprocessReader, ClusterDefinition clusterDefinition, NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(preprocessReader != null);
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            // Generate the master node variables in sorted order.  The variable 
            // names will be formatted as:
            //
            //      NEON_MASTER_#
            //
            // where [#] is the zero-based index of the node.  This is compatible
            // with the [getmanager] function included the script.
            //
            // Each variable defines an associative array with [name] and [address]
            // properties.
            //
            // Then generate the NEON_MASTER_NAMES and NEON_MASTER_ADDRESSES arrays.
            //
            // NOTE: We need to use Linux-style line endings.

            var sbMasters                  = new StringBuilder();
            var sbManagerNamesArray         = new StringBuilder();
            var sbManagerAddressesArray     = new StringBuilder();
            var sbPeerManagerAddressesArray = new StringBuilder();
            var sbManagerNodesSummary       = new StringBuilder();
            var index                       = 0;
            var managerNameWidth            = 0;

            sbManagerNamesArray.Append("(");
            sbManagerAddressesArray.Append("(");
            sbPeerManagerAddressesArray.Append("(");

            foreach (var manager in clusterDefinition.SortedManagers)
            {
                sbMasters.Append($"declare -x -A NEON_MASTER_{index}\n");
                sbMasters.Append($"NEON_MASTER_{index}=( [\"name\"]=\"{manager.Name}\" [\"address\"]=\"{manager.PrivateAddress}\" )\n");
                sbMasters.Append("\n");
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

            foreach (var manager in clusterDefinition.SortedManagers)
            {
                var nameField = manager.Name;

                if (nameField.Length < managerNameWidth)
                {
                    nameField += new string(' ', managerNameWidth - nameField.Length);
                }

                // The blanks below are just enough so that the "=" sign lines up
                // with the summary output from [cluster.conf.sh].

                if (sbManagerNodesSummary.Length == 0)
                {
                    sbManagerNodesSummary.Append($"    echo \"NEON_MASTER_NODES                 = {nameField}: {manager.PrivateAddress}\" 1>&2\n");
                }
                else
                {
                    sbManagerNodesSummary.Append($"    echo \"                                     {nameField}: {manager.PrivateAddress}\" 1>&2\n");
                }
            }

            foreach (var manager in clusterDefinition.SortedManagers)
            {
                sbMasters.Append($"declare -x -A NEON_MASTER_{index}\n");
                sbMasters.Append($"NEON_MASTER_{index}=( [\"name\"]=\"{manager.Name}\" [\"address\"]=\"{manager.PrivateAddress}\" )\n");
                index++;
            }

            sbMasters.Append("\n");
            sbMasters.Append($"declare -x NEON_MASTER_NAMES={sbManagerNamesArray}\n");
            sbMasters.Append($"declare -x NEON_MASTER_ADDRESSES={sbManagerAddressesArray}\n");

            sbMasters.Append("\n");

            if (clusterDefinition.Masters.Count() > 1)
            {
                sbMasters.Append($"declare -x NEON_MASTER_PEERS={sbPeerManagerAddressesArray}\n");
            }
            else
            {
                sbMasters.Append("export NEON_MASTER_PEERS=\"\"\n");
            }

            // Generate the manager and worker NTP time sources.

            var managerTimeSources = string.Empty;
            var workerTimeSources = string.Empty;

            if (clusterDefinition.TimeSources != null)
            {
                foreach (var source in clusterDefinition.TimeSources)
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

            foreach (var manager in clusterDefinition.SortedManagers)
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

            // Format the network upstream nameservers as semicolon separated
            // to be compatible with the PowerDNS Recursor [forward-zones-recurse]
            // configuration setting.
            //
            // Note that manager nodes will recurse to upstream (external) DNS 
            // servers and workers/pets will recurse to the managers so they can
            // dynamically pickup cluster DNS changes.

            if (clusterDefinition.Network?.Nameservers == null)
            {
                // $hack(jeff.lill): 
                //
                // [Network] will be null if we're just preparing servers, not doing full setup
                // so we'll set this to the defaults to avoid null references below.

                clusterDefinition.Network = new NetworkOptions();
            }

            var nameservers = string.Empty;

            if (nodeDefinition.Role == NodeRole.Master)
            {
                for (int i = 0; i < clusterDefinition.Network.Nameservers.Length; i++)
                {
                    if (i > 0)
                    {
                        nameservers += ";";
                    }

                    nameservers += clusterDefinition.Network.Nameservers[i].Trim();
                }
            }
            else
            {
                foreach (var manager in clusterDefinition.SortedManagers)
                {
                    if (nameservers.Length > 0)
                    {
                        nameservers += ";";
                    }

                    nameservers += manager.PrivateAddress;
                }
            }

            // Set the variables.

            preprocessReader.Set("load-cluster-conf", KubeHostFolders.Config + "/cluster.conf.sh --echo-summary");
            preprocessReader.Set("load-cluster-conf-quiet", KubeHostFolders.Config + "/cluster.conf.sh");

            SetBashVariable(preprocessReader, "cluster.provisioner", clusterDefinition.Provisioner);
            SetBashVariable(preprocessReader, "cluster.rootuser", Program.MachineUsername);

            SetBashVariable(preprocessReader, "node.driveprefix", clusterDefinition.DrivePrefix);

            SetBashVariable(preprocessReader, "neon.folders.archive", KubeHostFolders.Archive);
            SetBashVariable(preprocessReader, "neon.folders.bin", KubeHostFolders.Bin);
            SetBashVariable(preprocessReader, "neon.folders.exec", KubeHostFolders.Exec);
            SetBashVariable(preprocessReader, "neon.folders.config", KubeHostFolders.Config);
            SetBashVariable(preprocessReader, "neon.folders.scripts", KubeHostFolders.Scripts);
            SetBashVariable(preprocessReader, "neon.folders.secrets", KubeHostFolders.Secrets);
            SetBashVariable(preprocessReader, "neon.folders.setup", KubeHostFolders.Setup);
            SetBashVariable(preprocessReader, "neon.folders.source", KubeHostFolders.Source);
            SetBashVariable(preprocessReader, "neon.folders.state", KubeHostFolders.State);
            SetBashVariable(preprocessReader, "neon.folders.tmpfs", KubeHostFolders.Tmpfs);
            SetBashVariable(preprocessReader, "neon.folders.tools", KubeHostFolders.Tools);

            SetBashVariable(preprocessReader, "nodes.manager.count", clusterDefinition.Masters.Count());
            preprocessReader.Set("nodes.managers", sbMasters);
            preprocessReader.Set("nodes.manager.summary", sbManagerNodesSummary);

            SetBashVariable(preprocessReader, "ntp.manager.sources", managerTimeSources);
            NewMethod(preprocessReader, workerTimeSources);

            //-----------------------------------------------------------------
            // Configure the variables for the [setup-disk.sh] script.

            switch (clusterDefinition.Hosting.Environment)
            {
                case HostingEnvironments.Aws:

                    throw new NotImplementedException("$todo(jeff.lill)");

                case HostingEnvironments.Azure:

                    // The primary Azure data drive is [/dev/sdb] so any mounted drive will be [/dev/sdc].

                    if (nodeDefinition.Azure.HardDriveCount == 0)
                    {
                        SetBashVariable(preprocessReader, "data.disk", "PRIMARY");
                    }
                    else
                    {
                        SetBashVariable(preprocessReader, "data.disk", "/dev/sdc");
                    }
                    break;

                case HostingEnvironments.Google:

                    throw new NotImplementedException("$todo(jeff.lill)");

                case HostingEnvironments.HyperV:
                case HostingEnvironments.HyperVDev:
                case HostingEnvironments.Machine:
                case HostingEnvironments.Unknown:
                case HostingEnvironments.XenServer:

                    // VMs for all of these environments simply host their data on the
                    // primary OS disk only for now, the idea being that this disk
                    // can be sized up as necessary.  There are valid scenarios where
                    // folks would like the data on a different drive (e.g. for better
                    // performance).  I'm putting support for that on the backlog.

                    SetBashVariable(preprocessReader, "data.disk", "PRIMARY");
                    break;

                default:

                    throw new NotImplementedException($"The [{clusterDefinition.Hosting.Environment}] hosting environment is not implemented.");
            }
        }

        private static void NewMethod(PreprocessReader preprocessReader, string workerTimeSources)
        {
            SetBashVariable(preprocessReader, "ntp.worker.sources", workerTimeSources);
        }

        /// <summary>
        /// Uploads a resource file to the remote server after performing any necessary preprocessing.
        /// </summary>
        /// <typeparam name="TMetadata">The node metadata type.</typeparam>
        /// <param name="node">The remote node.</param>
        /// <param name="clusterDefinition">The cluster definition or <c>null</c>.</param>
        /// <param name="file">The resource file.</param>
        /// <param name="targetPath">The target path on the remote server.</param>
        private static void UploadFile<TMetadata>(this SshProxy<TMetadata> node, ClusterDefinition clusterDefinition, ResourceFiles.File file, string targetPath)
            where TMetadata : class
        {
            using (var input = file.ToStream())
            {
                if (file.HasVariables)
                {
                    // We need to expand any variables.  Note that if we don't have a
                    // cluster definition or for undefined variables, we're going to 
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

                            if (clusterDefinition != null)
                            {
                                SetHiveVariables(preprocessReader, clusterDefinition, node.Metadata as NodeDefinition);
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
        /// <param name="clusterDefinition">The cluster definition or <c>null</c>.</param>
        public static void UploadConfigFiles<Metadata>(this SshProxy<Metadata> node, ClusterDefinition clusterDefinition = null)
            where Metadata : class
        {
            Covenant.Requires<ArgumentNullException>(node != null);

            // Clear the contents of the configuration folder.

            node.Status = $"clear: {KubeHostFolders.Config}";
            node.SudoCommand($"rm -rf {KubeHostFolders.Config}/*.*");

            // Upload the files.

            node.Status = "upload: config files";

            foreach (var file in Program.LinuxFolder.GetFolder("conf").Files())
            {
                node.UploadFile(clusterDefinition, file, $"{KubeHostFolders.Config}/{file.Name}");
            }

            // Secure the files and make the scripts executable.

            node.SudoCommand($"chmod 644 {KubeHostFolders.Config}/*.*");
            node.SudoCommand($"chmod 744 {KubeHostFolders.Config}/*.sh");

            node.Status = "copied";
        }

        /// <summary>
        /// Uploads the setup and other scripts and tools for the target operating system to the server.
        /// </summary>
        /// <typeparam name="TMetadata">The server's metadata type.</typeparam>
        /// <param name="server">The remote server.</param>
        /// <param name="clusterDefinition">The cluster definition or <c>null</c>.</param>
        public static void UploadResources<TMetadata>(this SshProxy<TMetadata> server, ClusterDefinition clusterDefinition = null)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(server != null);

            //-----------------------------------------------------------------
            // Upload resource files to the setup folder.

            server.Status = $"clear: {KubeHostFolders.Setup}";
            server.SudoCommand($"rm -rf {KubeHostFolders.Setup}/*.*");

            // Upload the setup files.

            server.Status = "upload: setup files";

            foreach (var file in Program.LinuxFolder.GetFolder("setup").Files())
            {
                server.UploadFile(clusterDefinition, file, $"{KubeHostFolders.Setup}/{file.Name}");
            }

            // Make the setup scripts executable.

            server.SudoCommand($"chmod 744 {KubeHostFolders.Setup}/*");

            // Uncomment this if/when we have to upload source files.

#if FALSE
            //-----------------------------------------------------------------
            // Upload resource files to the source folder.  Note that we're going
            // to convert to Linux style line endings and we're going to convert
            // leading spaces into TABs (4 spaces == 1 TAB).

            // $hack(jeff.lill):
            //
            // This is hardcoded to assume that the source consists of a single level
            // folder with the source files.  If the folders nest eny further, we'll 
            // need to implement a recursive method to handle this properly.
            //
            // This code also assumes that the folder and file names do not include
            // any spaces.

            server.Status = $"clear: {HiveHostFolders.Source}";
            server.SudoCommand($"rm -rf {HiveHostFolders.Source}/*.*");

            // Upload the source files.

            server.Status = "upload: source files";

            foreach (var folder in Program.LinuxFolder.GetFolder("source").Folders())
            {
                foreach (var file in folder.Files())
                {
                    var targetPath = $"{HiveHostFolders.Source}/{folder.Name}/{file.Name}";

                    server.UploadText(targetPath, file.Contents, tabStop: -4);
                    server.SudoCommand("chmod 664", targetPath);
                }
            }
#endif

            //-----------------------------------------------------------------
            // Upload files to the tools folder.

            server.Status = $"clear: {KubeHostFolders.Tools}";
            server.SudoCommand($"rm -rf {KubeHostFolders.Tools}/*.*");

            // Upload the tool files.  Note that we're going to strip out the [.sh] 
            // file type to make these easier to run.

            server.Status = "upload: tool files";

            foreach (var file in Program.LinuxFolder.GetFolder("tools").Files())
            {
                server.UploadFile(clusterDefinition, file, $"{KubeHostFolders.Tools}/{file.Name.Replace(".sh", string.Empty)}");
            }

            // Make the scripts executable.

            server.SudoCommand($"chmod 744 {KubeHostFolders.Tools}/*");
        }
    }
}
