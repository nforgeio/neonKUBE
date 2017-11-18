//-----------------------------------------------------------------------------
// FILE:	    HyperVClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
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

            // Get the paths to any mounted VDHXs.

            var rawDrives = powershell.ExecuteTable($"Get-VM -Name \"{vm.Name}\" | Get-VMHardDiskDrive");

            foreach (dynamic rawDrive in rawDrives)
            {
                vm.DrivePaths.Add(rawDrive.Path);
            }

            return vm;
        }

        /// <summary>
        /// Creates a virtual machine. 
        /// </summary>
        /// <param name="name">The machine name.</param>
        /// <param name="memoryBytes">
        /// A string specifying the memory size.  This can be an integer byte count or an integer with
        /// units like <b>512MB</b> or <b>2GB</b>.  This defaults to <b>2GB</b>.
        /// </param>
        /// <param name="drivePath">
        /// The path where the virtual hard drive will be located.  Pass <c>null</c> to 
        /// have Hyper-V create the drive file or specify a path to the existing drive file
        /// to be used.
        /// </param>
        /// <param name="templateDrivePath">
        /// If this is specified and <paramref name="drivePath"/> is not <c>null</c> then
        /// the hard drive template at <paramref name="templateDrivePath"/> will be copied
        /// to <paramref name="drivePath"/> before creating the machine.
        /// </param>
        public void AddVM(string name, string memoryBytes = "2GB", string drivePath = null, string templateDrivePath = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            CheckDisposed();

            if (VMExists(name))
            {
                throw new HyperVException($"Virtual machine [{name}] already exists.");
            }

            // Copy the template VHDX file.

            if (drivePath != null && templateDrivePath != null)
            {
                File.Copy(templateDrivePath, drivePath);
            }

            // Create the virtual machine.

            if (drivePath == null)
            {
                powershell.Execute($"New-VM -Name \"{name}\" -MemoryStartupBytes {memoryBytes} -Generation 1 -SwitchName \"Default Switch\"");
            }
            else
            {
                powershell.Execute($"New-VM -Name \"{name}\" -MemoryStartupBytes {memoryBytes} -VHDPath \"{drivePath}\" -Generation 1 -SwitchName \"Default Switch\"");
            }
        }

        /// <summary>
        /// Removes a named virtual machine.
        /// </summary>
        /// <param name="name">The machine name.</param>
        public void RemoveVM(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            CheckDisposed();

            var machine = GetVM(name);

            // Remove the machine along with any of of its virtual hard drive files.

            powershell.Execute($"Remove-VM -Name \"{name}\"");

            foreach (var drivePath in machine.DrivePaths)
            {
                File.Decrypt(drivePath);
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
        /// <param name="name">The machine name.</param>
        /// <returns>The <see cref="VirtualMachine"/>.</returns>
        public VirtualMachine GetVM(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            CheckDisposed();

            var machines = new List<VirtualMachine>();
            var table = powershell.ExecuteTable($"Get-VM -Name \"{name}\"");

            Covenant.Assert(table.Count == 1);

            return ExtractVM(table.First());
        }

        /// <summary>
        /// Determines whether a named virtual machine exists.
        /// </summary>
        /// <param name="name">The machine name.</param>
        /// <returns><c>true</c> if the machine exists.</returns>
        public bool VMExists(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            CheckDisposed();

            return ListVMs().Count(vm => vm.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) > 0;
        }

        /// <summary>
        /// Starts the named virtual machine.
        /// </summary>
        /// <param name="name">The machine name.</param>
        public void StartVM(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            CheckDisposed();

            powershell.Execute($"Start-VM -Name \"{name}\"");
        }

        /// <summary>
        /// Stops the named virtual machine.
        /// </summary>
        /// <param name="name">The machine name.</param>
        public void StopVM(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            CheckDisposed();

            powershell.Execute($"Stop-VM -Name \"{name}\"");
        }
    }
}
