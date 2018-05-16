//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDashboard.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Consul;

using Neon.Cluster;
using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Cluster;

using Xunit;

namespace TestNeonCluster
{
    public class Test_AnsibleDashboard : IClassFixture<ClusterFixture>
    {
        //---------------------------------------------------------------------
        // Static members

        private static int dashboardId = 0;

        //---------------------------------------------------------------------
        // Instance members

        private ClusterFixture cluster;

        public Test_AnsibleDashboard(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // We're going to use unique dashboard name for each test
            // so we only need to reset the test fixture once for
            // all tests implemented by this class.

            cluster.LoginAndInitialize(login: null);
        }

        /// <summary>
        /// Returns a unique dashboard name for testing.
        /// </summary>
        /// <returns></returns>
        private string GetDashboardName()
        {
            return $"test-{dashboardId++}";
        }

        /// <summary>
        /// Returns the entry for a cluster dashboard.
        /// </summary>
        /// <param name="name">The dashboard name.</param>
        /// <returns>The <see cref="ClusterDashboard"/> or <c>null</c>.</returns>
        private ClusterDashboard GetDashboard(string name)
        {
            return cluster.Consul.KV.GetObjectOrDefault<ClusterDashboard>($"{NeonClusterConst.ConsulDashboardsKey}/{name}").Result;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CheckArgs()
        {
            // Verify that we can detect unknown top-level arguments.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dashboard
      neon_dashboard:
        state: present
        name: google
        title: Google Search
        folder: Test
        url: https://google.com
        description: Search the web
        UNKNOWN: argument
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage dashboard");

            Assert.False(taskResult.Success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create()
        {
            var name = GetDashboardName();

            // Should start out without this dashboard.

            Assert.Null(GetDashboard(name));

            //-----------------------------------------------------------------
            // Create a dashboard and then verify that it was added.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dashboard
      neon_dashboard:
        state: present
        name: {name}
        title: Google Search
        folder: Test-Folder
        url: https://google.com/
        description: Search the web
";
            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("manage dashboard");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var dashboard = GetDashboard(name);

            Assert.NotNull(dashboard);
            Assert.Equal(name, dashboard.Name);
            Assert.Equal("Google Search", dashboard.Title);
            Assert.Equal("Test-Folder", dashboard.Folder);
            Assert.Equal("https://google.com/", dashboard.Url);
            Assert.Equal("Search the web", dashboard.Description);

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // be changed because the dashboard already exists.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dashboard");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            dashboard = GetDashboard(name);

            Assert.NotNull(dashboard);
            Assert.Equal(name, dashboard.Name);
            Assert.Equal("Google Search", dashboard.Title);
            Assert.Equal("Test-Folder", dashboard.Folder);
            Assert.Equal("https://google.com/", dashboard.Url);
            Assert.Equal("Search the web", dashboard.Description);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update()
        {
            var name = GetDashboardName();

            // Should start out without this dashboard.

            Assert.Null(GetDashboard(name));

            //-----------------------------------------------------------------
            // Create a dashboard and then verify that it was added.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dashboard
      neon_dashboard:
        state: present
        name: {name}
        title: Google Search
        folder: Test-Folder
        url: https://google.com/
        description: Search the web
";
            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("manage dashboard");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var dashboard = GetDashboard(name);

            Assert.NotNull(dashboard);
            Assert.Equal(name, dashboard.Name);
            Assert.Equal("Google Search", dashboard.Title);
            Assert.Equal("Test-Folder", dashboard.Folder);
            Assert.Equal("https://google.com/", dashboard.Url);
            Assert.Equal("Search the web", dashboard.Description);

            //-----------------------------------------------------------------
            // Modify the dashboard and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dashboard
      neon_dashboard:
        state: present
        name: {name}
        title: Google Search NEW
        folder: Test-Folder-NEW
        url: https://google.com/new
        description: Search the web NEW
";
            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dashboard");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dashboard = GetDashboard(name);

            Assert.NotNull(dashboard);
            Assert.Equal(name, dashboard.Name);
            Assert.Equal("Google Search NEW", dashboard.Title);
            Assert.Equal("Test-Folder-NEW", dashboard.Folder);
            Assert.Equal("https://google.com/new", dashboard.Url);
            Assert.Equal("Search the web NEW", dashboard.Description);

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // have changed.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dashboard");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            dashboard = GetDashboard(name);

            Assert.NotNull(dashboard);
            Assert.Equal(name, dashboard.Name);
            Assert.Equal("Google Search NEW", dashboard.Title);
            Assert.Equal("Test-Folder-NEW", dashboard.Folder);
            Assert.Equal("https://google.com/new", dashboard.Url);
            Assert.Equal("Search the web NEW", dashboard.Description);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Remove()
        {
            var name = GetDashboardName();

            // Should start out without this dashboard.

            Assert.Null(GetDashboard(name));

            //-----------------------------------------------------------------
            // Create a dashboard and then verify that it was added.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dashboard
      neon_dashboard:
        state: present
        name: {name}
        title: Google Search
        folder: Test-Folder
        url: https://google.com/
        description: Search the web
";
            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("manage dashboard");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var dashboard = GetDashboard(name);

            Assert.NotNull(dashboard);
            Assert.Equal(name, dashboard.Name);
            Assert.Equal("Google Search", dashboard.Title);
            Assert.Equal("Test-Folder", dashboard.Folder);
            Assert.Equal("https://google.com/", dashboard.Url);
            Assert.Equal("Search the web", dashboard.Description);

            //-----------------------------------------------------------------
            // Remove it and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dashboard
      neon_dashboard:
        state: absent
        name: {name}
";
            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dashboard");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Null(GetDashboard(name));

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // be changed because the was already deleted.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dashboard");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Null(GetDashboard(name));
        }
    }
}
