//-----------------------------------------------------------------------------
// FILE:	    HyperVClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Cluster.HyperV
{
    /// <summary>
    /// <para>
    /// Abstracts management of local Hyper-V virtual machines and components
    /// on Windows via PowerShell.
    /// </para>
    /// <note>
    /// This class requires elevated administrative rights.
    /// </note>
    /// </summary>
    /// <threadsafety instance="false"/>
    public class HyperVClient : IDisposable
    {
        private PowerShell      powershell;

        /// <summary>
        /// Default constructor to be used to manage Hyper-V objects
        /// on the local Windows machine.
        /// </summary>
        public HyperVClient()
        {
            if (!NeonHelper.IsWindows)
            {
                throw new NotSupportedException($"{nameof(HyperVClient)} is only supported on Windows.");
            }

            powershell = new PowerShell();
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (powershell != null)
            {
                powershell.Dispose();
                powershell = null;
            }
        }

        /// <summary>
        /// Ensures that the instance has not been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
        private void CheckDisposed()
        {
            if (powershell == null)
            {
                throw new ObjectDisposedException(nameof(HyperVClient));
            }
        }

        /// <summary>
        /// Extracts virtual machine properties from a dynamic PowerShell result.
        /// </summary>
        /// <param name="rawMachine">The dynamic machine properties.</param>
        /// <returns>The parsed <see cref="VirtualMachine"/>.</returns>
        private VirtualMachine ExtractVM(dynamic rawMachine)
        {
            var vm = new VirtualMachine();

            vm.Name = rawMachine.Name;

            switch ((string)rawMachine.State)
            {
                case "Off":

                    vm.State = VirtualMachineState.Off;
                    break;

                case "Starting":

                    vm.State = VirtualMachineState.Starting;
                    break;

                case "Running":

                    vm.State = VirtualMachineState.Running;
                    break;

                case "Paused":

                    vm.State = VirtualMachineState.Paused;
                    break;

                case "Saved":

                    vm.State = VirtualMachineState.Saved;
                    break;

                default:

                    vm.State = VirtualMachineState.Unknown;
                    break;
            }

            return vm;
        }

        /// <summary>
        /// Creates a virtual machine. 
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <param name="memorySize">
        /// A string specifying the memory size.  This can be an integer byte count or an integer with
        /// units like <b>512MB</b> or <b>2GB</b>.  This defaults to <b>2GB</b>.
        /// </param>
        /// <param name="minimumMemorySize">
        /// Optionally specifies the minimum memory size.  This defaults to <c>null</c> which will
        /// set this to <paramref name="memorySize"/>.
        /// </param>
        /// <param name="processorCount">
        /// The number of virutal processors to assign to the machine.  This defaults to <b>4</b>.</param>
        /// <param name="drivePath">
        /// Optionally specifies the path where the virtual hard drive will be located.  Pass 
        /// <c>null</c> to  have Hyper-V create the drive file or specify a path to the existing 
        /// drive file to be used.
        /// </param>
        /// <param name="checkpointDrive">Optionally enables drive checkpoints.  This defaults to <c>false</c>.</param>
        /// <param name="templateDrivePath">
        /// If this is specified and <paramref name="drivePath"/> is not <c>null</c> then
        /// the hard drive template at <paramref name="templateDrivePath"/> will be copied
        /// to <paramref name="drivePath"/> before creating the machine.
        /// </param>
        /// <param name="switchName">Optional name of the virtual switch.</param>
        public void AddVM(
            string  machineName, 
            string  memorySize = "2GB", 
            string  minimumMemorySize = null, 
            int     processorCount = 4, 
            string  drivePath = null,
            bool    checkpointDrive = false,
            string  templateDrivePath = null, 
            string  switchName = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(machineName));
            CheckDisposed();

            if (string.IsNullOrEmpty(minimumMemorySize))
            {
                minimumMemorySize = memorySize;
            }

            if (VMExists(machineName))
            {
                throw new HyperVException($"Virtual machine [{machineName}] already exists.");
            }

            // Copy the template VHDX file.

            if (!string.IsNullOrEmpty(drivePath) && templateDrivePath != null)
            {
                File.Copy(templateDrivePath, drivePath);
            }

            // Create the virtual machine.

            var command = $"New-VM -Name \"{machineName}\" -MemoryStartupBytes {minimumMemorySize} -Generation 1";

            if (!string.IsNullOrEmpty(drivePath))
            {
                command += $" -VHDPath \"{drivePath}\"";
            }

            if (!string.IsNullOrEmpty(switchName))
            {
                command += $" -SwitchName \"{switchName}\"";
            }

            powershell.Execute(command);

            // We need to configure the VM's processor count and min/max memory settings.

            powershell.Execute($"Set-VM -Name \"{machineName}\" -ProcessorCount {processorCount} -MemoryMinimumBytes {minimumMemorySize} -MemoryMaximumBytes {memorySize}");

            // Builds of Windows 10 since the August 2017 Creators Update enable automatic
            // virtual drive checkpointing (which is annoying).

            if (!checkpointDrive)
            {
                powershell.Execute($"Set-VM -CheckpointType Disabled -AutomaticCheckpointsEnabled 0 -Name \"{machineName}\"");
            }
        }

        /// <summary>
        /// Removes a named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        public void RemoveVM(string machineName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(machineName));
            CheckDisposed();

            var machine = GetVM(machineName);
            var drives  = GetVMDrives(machineName);

            // Remove the machine along with any of of its virtual hard drive files.

            powershell.Execute($"Remove-VM -Name \"{machineName}\" -Force");

            foreach (var drivePath in drives)
            {
                File.Delete(drivePath);
            }
        }

        /// <summary>
        /// Lists the virtual machines.
        /// </summary>
        /// <returns><see cref="IEnumerable{VirtualMachine}"/>.</returns>
        public IEnumerable<VirtualMachine> ListVMs()
        {
            CheckDisposed();

            var machines = new List<VirtualMachine>();
            var table    = powershell.ExecuteTable("Get-VM");

            foreach (dynamic rawMachine in table)
            {
                machines.Add(ExtractVM(rawMachine));
            }

            return machines;
        }

        /// <summary>
        /// Gets the current status for a named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <returns>The <see cref="VirtualMachine"/>.</returns>
        public VirtualMachine GetVM(string machineName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(machineName));
            CheckDisposed();

            var machines = new List<VirtualMachine>();
            var table = powershell.ExecuteTable($"Get-VM -Name \"{machineName}\"");

            Covenant.Assert(table.Count == 1);

            return ExtractVM(table.First());
        }

        /// <summary>
        /// Determines whether a named virtual machine exists.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <returns><c>true</c> if the machine exists.</returns>
        public bool VMExists(string machineName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(machineName));
            CheckDisposed();

            return ListVMs().Count(vm => vm.Name.Equals(machineName, StringComparison.InvariantCultureIgnoreCase)) > 0;
        }

        /// <summary>
        /// Starts the named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        public void StartVM(string machineName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(machineName));
            CheckDisposed();

            powershell.Execute($"Start-VM -Name \"{machineName}\"");
        }

        /// <summary>
        /// Stops the named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        public void StopVM(string machineName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(machineName));
            CheckDisposed();

            powershell.Execute($"Stop-VM -Name \"{machineName}\"");
        }

        /// <summary>
        /// Returns host file system paths to any virtual drives attached to
        /// the named virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <returns>The list of fully qualified virtual drive file paths.</returns>
        public List<string> GetVMDrives(string machineName)
        {
            var drives = new List<string>();
            var rawDrives = powershell.ExecuteTable($"Get-VMHardDiskDrive -VMName \"{machineName}\"");

            foreach (dynamic rawDrive in rawDrives)
            {
                drives.Add(rawDrive.Path);
            }

            return drives;
        }

        /// <summary>
        /// Returns the virtual network switches.
        /// </summary>
        /// <returns>The list of switches.</returns>
        public List<VirtualSwitch> ListVMSwitches()
        {
            var switches    = new List<VirtualSwitch>();
            var rawSwitches = powershell.ExecuteTable($"Get-VMSwitch");

            foreach (dynamic rawSwitch in rawSwitches)
            {
                var virtualSwitch
                    = new VirtualSwitch()
                    {
                        Name = rawSwitch.Name
                    };

                switch (rawSwitch.SwitchType)
                {
                    case "Internal":

                        virtualSwitch.Type = VirtualSwitchType.Internal;
                        break;

                    case "External":

                        virtualSwitch.Type = VirtualSwitchType.External;
                        break;

                    case "Private":

                        virtualSwitch.Type = VirtualSwitchType.Private;
                        break;

                    default:

                        virtualSwitch.Type = VirtualSwitchType.Unknown;
                        break;
                }

                switches.Add(virtualSwitch);
            }

            return switches;
        }

        /// <summary>
        /// Adds a virtual ethernet switch to Hyper-V with external connectivity
        /// to the ethernet adapter named <b>Ethernet</b>.
        /// </summary>
        /// <param name="switchName">The new switch name.</param>
        public void NewVMExternalSwitch(string switchName)
        {
            // $todo(jeff.lill):
            //
            // This may be fragile because it assumes that an ethernet adapter
            // named "Ethernet" exists and is actually the correct adapter.

            var adapters             = powershell.ExecuteTable($"Get-NetAdapter");
            var interfaceDescription = (string)null;

            foreach (dynamic adapter in adapters)
            {
                if (((string)adapter.Name).Equals("Ethernet", StringComparison.InvariantCultureIgnoreCase))
                {
                    interfaceDescription = adapter.InterfaceDescription;
                    break;
                }
            }

            if (interfaceDescription == null)
            {
                throw new HyperVException($"Cannot create the [{switchName}] virtual external switch because the [Ethernet] network adapter doesn't exist.");
            }

            powershell.Execute($"New-VMSwitch -Name \"{switchName}\" -NetAdapterInterfaceDescription \"{interfaceDescription}\"");
        }

        /// <summary>
        /// Returns the virtual network adapters attached to the name virtual machine.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <param name="waitForAddresses">Optionally waits until the adapter has been able to acquire at least one IPv4 address.</param>
        /// <returns>The list of network adapters.</returns>
        public List<VirtualNetworkAdapter> ListVMNetworkAdapters(string machineName, bool waitForAddresses = false)
        {
            var stopwatch = new Stopwatch();

            while (true)
            {
                var adapters    = new List<VirtualNetworkAdapter>();
                var rawAdapters = powershell.ExecuteTable($"Get-VMNetworkAdapter -VMName \"{machineName}\"");

                adapters.Clear();

                foreach (dynamic rawAdapter in rawAdapters)
                {
                    var adapter
                        = new VirtualNetworkAdapter()
                        {
                            Name           = rawAdapter.Name,
                            VMName         = rawAdapter.VMName,
                            IsManagementOs = ((string)rawAdapter.IsManagementOs).Equals("True", StringComparison.InvariantCultureIgnoreCase),
                            SwitchName     = rawAdapter.SwitchName,
                            MacAddress     = rawAdapter.MacAddress,
                            Status         = rawAdapter.Status
                        };

                    // Parse the IP addresses.

                    string addresses = rawAdapter.IPAddresses;

                    if (addresses.Length >= 2)
                    {
                        // Strip the curly braces off both ends of the field.

                        addresses = addresses.Substring(1, addresses.Length - 2);

                        foreach (var address in addresses.Split(','))
                        {
                            if (!string.IsNullOrEmpty(address))
                            {
                                var ipAddress = IPAddress.Parse(address.Trim());

                                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    adapter.Addresses.Add(IPAddress.Parse(address.Trim()));
                                }
                            }
                        }
                    }

                    adapters.Add(adapter);
                }

                var retry = false;

                foreach (var adapter in adapters)
                {
                    if (adapter.Addresses.Count == 0 && waitForAddresses)
                    {
                        if (stopwatch.Elapsed >= TimeSpan.FromSeconds(30))
                        {
                            throw new TimeoutException($"Network adapter [{adapter.Name}] for virtual machine [{machineName}] was not able to acquire an IP address.");
                        }

                        retry = true;
                        break;
                    }
                }

                if (retry)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    continue;
                }

                return adapters;
            }
        }
    }
}
