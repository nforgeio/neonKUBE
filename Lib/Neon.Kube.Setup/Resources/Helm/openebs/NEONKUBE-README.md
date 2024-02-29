# Upgrade Instructions

The basic idea here is to download the Helm chart for the version of
OpenEBS being installed, replace all of the existing chart files (except
for these instructions), and then edit the **values.yaml** file as
required.

1. Set **KubeVersion.OpenEbs** to the desired version.

2. Execute this script to download the OpenEBS Helm chart defined by **KubeVersion.OpenEbs**.
   
   ```
   pwsh -f "%NK_ROOT%\Lib\Neon.Kube.Setup\Resources\Helm\openebs\upgrade.ps1"
   ```

   **NOTE:** This replaces all existing chart files with the assumption that
   most, if not all, customization is performed by by customizing a values
   file in code when installing the Helm chart, ovwerriding the chart's
   [values.yaml] file and we are not going to modify this chart file.

3. Open the NEONCLOUD solution in Visual Studio and review/edit the **Git Changes**.

   We're going to try having all values specified as parameters when installing
   the Helm chart rather than editing the chart's **values.yaml** file directly.

   We're going to avoid modifying chart template or other files to make upgrades
   easier.  If we **REALLY** need to make changes to these files, please add a 
   comment that starts with **"NEONKUBE:"** calling out the change.

4. Diff **values.yaml** to look for new or removed values and also to look
   for container image/version changes.  Also look for new container references.
   Update **KubeVersions.cs** and the NEONKUBE image build as required.

5. Compare the chart's **values.yaml** file with the values override file
   generated within **InstallOpenEbsAsync()** in `KubeSetup.Operations.cs`;
   you'll need to set a breakpoint after the `var values = preprocessor.ReadToEnd();` 
   call and then save the `values` variable string to a temporary file somewhere
   (like `%TEMP%\values.yaml`).

   You can use the Linux **diff** command to compare the chart's values file 
   with the temporary file:

   ```
   diff -y "%NK_ROOT\Lib\Neon.Kube.Setup\Resources\Helm\openebs\values.yaml" "%TEMP%\values.yaml"
   ```

   This will display a side-by-side diff.

   Another (perhaps nicer) approach is to:

   a. Commit the changes to the `%NK_ROOT%\Lib\Neon.Kube.Setup\Resources\Helm\openebs`
      after running the upgrade script
   b. Replace the chart's **values.yaml** file with the preprocessed text
      obtained via the debugger above
   c. Use Visual Studio to review the differences and make any necessary
      changes to the chart's **values.yaml** file
   d. When you're done, copy contents of the chart's **values.yaml**
      file and then paste that into the **values** variable in **InstallOpenEbsAsync()**,
      replacing the entire string
   e. **IMPORTANT: UNDO ALL CHANGES TO THE CHART'S VALUES FILE** (this needs
      to revert to the stock values file).

6. Review the Helm chart template and other files for changes we need to apply.

   **Limits/Requests:** Add any required limits/requests to the values override string.

7. Deploy clusters with various configuration options (Jiva, cStor,...), breaking
   into the debugger after the OpenEBS Helm chart has been installed.

   a. Fix any container image reference failures
   b. Run `neon cluster check --container-images`, looking for container
      images that are not referenced from the local container registry or
      are not in the cluster manifest.  You'll need to fix `KubeVersion`
      constants and image builds until this is clean.
   c. Run `neon cluster check --priority-class`, looking for any OpenEBS
      pods without a **PriorityClass** specification.  You'll need to
      modify override **values** string until this is clean.
