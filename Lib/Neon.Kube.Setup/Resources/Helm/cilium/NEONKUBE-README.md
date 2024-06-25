# Upgrade Instructions

The basic idea here is to download the Helm chart for the version of
Cilium being installed, replace all of the existing chart files (except
for these instructions and the upgrade script), and then edit the
**values.yaml** file as required.

Here's how this is accomplished:

1. Set **KubeVersion.Cilium** to the desired version.

2. Execute this script to download the Cilium Helm chart defined by **KubeVersion.Cilium**.
   
   ```
   pwsh -f "%NK_ROOT%\Lib\Neon.Kube.Setup\Resources\Helm\cilium\upgrade.ps1"
   ```

   **NOTE:** This replaces all existing chart files with the assumption that
   most, if not all, customization is performed by by customizing the values
   in code when installing the Helm chart.

3. Open the NeonCLOUD solution in Visual Studio and review/edit the **Git Changes**.

   We're going to try having all values specified as parameters when installing
   the Helm chart rather than editing the chart's **values.yaml** file directly.

   We're going to avoid modifying chart template or other files to make upgrades
   easier.  If we **REALLY** need to make changes to these files, please add a 
   comment like **# NeonKUBE CUSTOM VALUE** comment calling out these changes.

   We may also need to add custom templates for some services.  We're going to
   add the **# NeonKUBE CUSTOM TEMPLATE** comment at the top of these files.

4. Review the Helm chart template and other files for changes we need to apply.

   **Limits/Requests:** Add any required limits/requests to the values override string.

5. Deploy clusters with various configuration options, breaking into the 
   debugger after the Cilium Helm chart has been installed.

   a. Fix any container image reference failures
   b. Run `neon cluster check --container-images`, looking for container
      images that are not referenced from the local container registry or
      are not in the cluster manifest.  You'll need to fix `KubeVersion`
      constants and image builds until this is clean.
   c. Run `neon cluster check --priority-class`, looking for any OpenEBS
      pods without a **PriorityClass** specification.  You'll need to
      modify override **values** string until this is clean.
