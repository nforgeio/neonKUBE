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
   most, if not all, customization is performed by passing custom values in
   code when installing the Helm chart.

3. Open the NEONCLOUD solution in Visual Studio and review/edit the **Git Changes**.

   We're going to try having all values specified as parameters when installing
   the Helm chart rather than editing the chart's **values.yaml** file directly.

   If we need to make changes to templates or other files, please add a comment
   that starts with **"NEONKUBE:"** calling out the change.

4. Diff **README.md** to look for new or removed values and also to look
   for container image/version changes.  Update **KubeVersions.cs** and
   the NEONKUBE image build as required.
