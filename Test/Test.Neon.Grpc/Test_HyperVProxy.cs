//-----------------------------------------------------------------------------
// FILE:        Test_HyperVProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright � 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by NEONFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with NEONFORGE, LLC.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Deployment;
using Neon.HyperV;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.DesktopService;
using Neon.Kube.Hosting.HyperV;
using Neon.Net;
using Neon.Xunit;

using Xunit;

namespace TestGrpc
{
    /// <summary>
    /// These tests verify that the internal <see cref="HyperVProxy"/> class from the <b>Neon.Kube.HyperV</b>
    /// library is able to perform <see cref="HyperVClient"/> operations directly when running in simulated 
    /// admin mode as well indirectly via the neon desktop server when running in simulated non-admin mode.
    /// </summary>
    [Trait(TestTrait.Category, TestArea.NeonHyperV)]
    public class Test_HyperVProxy
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used when running a test in admin mode.
        /// </summary>
        private class DoNothingDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string TestMachineName1 = "test-hypervproxy-1";
        private const string TestMachineName2 = "test-hypervproxy-2";
        private const string TestSwitchName   = "test-hypervproxy";

        private static readonly string socketPath   = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), $"hypervproxy-test.sock");
        private static readonly string templatePath = TestHelper.GetUbuntuTestVhdxPath();

        /// <summary>
        /// Constructor.
        /// </summary>
        public Test_HyperVProxy()
        {
            ClearState();
        }

        /// <summary>
        /// Clears any test related state.
        /// </summary>
        private void ClearState()
        {
            // Put Hyper-V into a known state by ensuring that any test related assets
            // left over from a previous run are removed.

            using (var hyperv = new HyperVClient())
            {
                if (hyperv.FindVm(machineName: TestMachineName1) != null)
                {
                    hyperv.StopVm(TestMachineName1, turnOff: true);
                    hyperv.RemoveVm(TestMachineName1);
                }

                if (hyperv.FindVm(machineName: TestMachineName2) != null)
                {
                    hyperv.StopVm(TestMachineName2, turnOff: true);
                    hyperv.RemoveVm(TestMachineName2);
                }

                if (hyperv.FindSwitch(switchName: TestSwitchName) != null)
                {
                    hyperv.RemoveSwitch(TestSwitchName);
                }
            }
        }

        /// <summary>
        /// Creates and returns a <see cref="DesktopService"/> when not running in admin mode
        /// to verify that <see cref="HyperVProxy"/> is able to successfully submit requests to 
        /// that service.  This returns a <see cref="DoNothingDisposable"/> for non-admin mode
        /// to verify that <see cref="HyperVProxy"/> can call <see cref="HyperVClient"/> directly.
        /// </summary>
        /// <param name="isAdmin">Pass <c>true</c> for simulated admin mode.</param>
        /// <returns>An <see cref="IDisposable"/> that represents the simulated service.</returns>
        private IDisposable CreateService(bool isAdmin)
        {
            if (isAdmin)
            {
                return new DoNothingDisposable();
            }
            else
            {
                return new DesktopService(socketPath);
            }
        }

        [MaintainerTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetWindowsOptionalFeatures(bool isAdmin)
        {
            using (CreateService(isAdmin))
            {
                var hyperVProxy = new HyperVProxy(isAdmin, socketPath);
                var features    = hyperVProxy.GetWindowsOptionalFeatures();

                Assert.NotEmpty(features);
            }
        }

        [MaintainerTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsNestedVirtualization(bool isAdmin)
        {
            using (CreateService(isAdmin))
            {
                var hyperVProxy = new HyperVProxy(isAdmin, socketPath);
                var isNested    = hyperVProxy.IsNestedVirtualization;

#pragma warning disable CA1416
                Assert.Equal(global::Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Virtual Machine\Auto", "OSName", null) != null, isNested);
#pragma warning restore CA1416
            }
        }

        [MaintainerTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void VirtualMachines(bool isAdmin)
        {
            try
            {
                using (var tempFolder = new TempFolder())
                {
                    using (CreateService(isAdmin))
                    {
                        var hyperVProxy = new HyperVProxy(isAdmin, socketPath);

                        // List VMs before we create any below.  We had an issue once where we'd see
                        // a [NullReferenceException] when there were no VMs.

                        var vms = hyperVProxy.ListVms();

                        vms = hyperVProxy.ListVms();

                        // Scan for a switch with external access creating one if necessary.

                        var externalSwitchName = hyperVProxy.ListSwitches()
                            .FirstOrDefault(@switch => @switch.Type == VirtualSwitchType.External)
                            ?.Name;

                        if (externalSwitchName == null)
                        {
                            externalSwitchName = "External";

                            var connectedInterface = NetHelper.GetConnectedInterface();

                            hyperVProxy.NewExternalSwitch(externalSwitchName, NetHelper.GetConnectedGatewayAddress());
                        }

                        foreach (var @switch in hyperVProxy.ListSwitches())
                        {
                            if (@switch.Type == VirtualSwitchType.External)
                            {
                                externalSwitchName = @switch.Name;
                                break;
                            }
                        }

                        // Create a VM and verify.

                        var test1VhdxPath = Path.Combine(tempFolder.Path, "boot1.vhdx");

                        hyperVProxy.AddVm(
                            machineName:       TestMachineName1,
                            memorySize:        "1 GiB",
                            processorCount:    2,
                            drivePath:         test1VhdxPath,
                            checkpointDrives:  false,
                            templateDrivePath: templatePath,
                            switchName:        externalSwitchName);

                        var vm = hyperVProxy.FindVm(machineName: TestMachineName1);

                        Assert.NotNull(vm);
                        Assert.Equal(TestMachineName1, vm.Name);
                        Assert.Equal(VirtualMachineState.Off, vm.State);
                        Assert.Equal(externalSwitchName, vm.SwitchName);

                        // Start the VM and verify.

                        hyperVProxy.StartVm(machineName: TestMachineName1);

                        vm = hyperVProxy.FindVm(machineName: TestMachineName1);

                        Assert.NotNull(vm);
                        Assert.Equal(VirtualMachineState.Running, vm.State);

                        // Fetch the VM network adapters.

                        var adapters = hyperVProxy.ListVmNetworkAdapters(TestMachineName1);

                        Assert.NotNull(adapters);
                        Assert.NotEmpty(adapters);

                        // Save the VM and verify.

                        hyperVProxy.SaveVm(machineName: TestMachineName1);

                        vm = hyperVProxy.FindVm(machineName: TestMachineName1);

                        Assert.NotNull(vm);
                        Assert.Equal(VirtualMachineState.Saved, vm.State);

                        // Create and start another VM and verify.

                        var test2VhdxPath = Path.Combine(tempFolder.Path, "boot2.vhdx");

                        hyperVProxy.AddVm(
                            machineName:       TestMachineName2,
                            memorySize:        "1 GiB",
                            processorCount:    4,
                            drivePath:         test2VhdxPath,
                            checkpointDrives:  false,
                            templateDrivePath: templatePath,
                            switchName:        externalSwitchName);

                        vm = hyperVProxy.FindVm(machineName: TestMachineName2);

                        Assert.NotNull(vm);
                        Assert.Equal(TestMachineName2, vm.Name);
                        Assert.Equal(VirtualMachineState.Off, vm.State);
                        Assert.Equal(externalSwitchName, vm.SwitchName);

                        hyperVProxy.StartVm(machineName: TestMachineName2);

                        vm = hyperVProxy.FindVm(machineName: TestMachineName2);

                        Assert.Equal(VirtualMachineState.Running, vm.State);

                        // List and check the VM existence.

                        var list = hyperVProxy.ListVms();

                        Assert.Contains(list, item => item.Name == TestMachineName1);
                        Assert.Contains(list, item => item.Name == TestMachineName2);
                        Assert.True(hyperVProxy.VmExists(TestMachineName1));
                        Assert.True(hyperVProxy.VmExists(TestMachineName2));
                        Assert.False(hyperVProxy.VmExists(Guid.NewGuid().ToString("d")));

                        // Test DVD/CD insert and eject operations.

                        var isoFolder = Path.Combine(tempFolder.Path, "iso-contents");
                        var isoPath   = Path.Combine(tempFolder.Path, "data.iso");

                        Directory.CreateDirectory(isoFolder);
                        File.WriteAllText(Path.Combine(isoFolder, "hello.txt"), "HELLO WORLD!");
                        KubeHelper.CreateIsoFile(isoFolder, isoPath);

                        hyperVProxy.InsertVmDvd(TestMachineName2, isoPath);
                        hyperVProxy.EjectVmDvd(TestMachineName2);

                        // Stop the second VM and verify.

                        hyperVProxy.StopVm(machineName: TestMachineName2, turnOff: true);

                        vm = hyperVProxy.FindVm(machineName: TestMachineName2);

                        Assert.Equal(VirtualMachineState.Off, vm.State);

                        // Remove the VMs and verify.

                        hyperVProxy.RemoveVm(machineName: TestMachineName1, keepDrives: false);

                        Assert.Null(hyperVProxy.FindVm(machineName: TestMachineName1));
                    }
                }
            }
            finally
            {
                ClearState();
            }
        }

        [MaintainerTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void Switches(bool isAdmin)
        {
            try
            {
                using (CreateService(isAdmin))
                {
                    var hyperVProxy = new HyperVProxy(isAdmin, socketPath);

                    // List existing switches.

                    var switches = hyperVProxy.ListSwitches();

                    Assert.NotEmpty(switches);

                    foreach (var item in switches)
                    {
                        var @switch = hyperVProxy.FindSwitch(item.Name);

                        Assert.NotNull(@switch);
                        Assert.Equal(item.Name, @switch.Name);
                    }

                    // $todo(jefflill):
                    //
                    // There isn't an easy way to test switch creation without the serious
                    // possibility of messing with the Hyper-V configuration so we'll defer
                    // this until the future.
                }
            }
            finally
            {
                ClearState();
            }
        }

        [MaintainerTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void Nats(bool isAdmin)
        {
            try
            {
                using (CreateService(isAdmin))
                {
                    var hyperVProxy = new HyperVProxy(isAdmin, socketPath);

                    // We're just going to list NATs because we don't want mess with
                    // creating new swiches right now.

                    var nats = hyperVProxy.ListNats();

                    foreach (var item in nats)
                    {
                        var nat = hyperVProxy.FindNatByName(item.Name);

                        Assert.NotNull(nat);
                        Assert.Equal(item.Name, nat.Name);
                        Assert.Equal(item.Subnet, nat.Subnet);

                        nat = hyperVProxy.FindNatBySubnet(item.Subnet);

                        Assert.NotNull(nat);
                        Assert.Equal(item.Name, nat.Name);
                        Assert.Equal(item.Subnet, nat.Subnet);
                    }
                }
            }
            finally
            {
                ClearState();
            }
        }
    }
}
