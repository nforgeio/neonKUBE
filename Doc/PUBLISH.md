# Steps to publish the project NuGet packages:

1. Open the **Properties** each of the library projects and update the **Release notes**.

3. Update `$/nuget-version.txt` (or `GitHub/nuget-version.txt` in the solution) with the 
   new package version.

4. Manually clean and rebuild the entire solution: RELEASE configuration.

5. Execute `$/Toolbin/nuget-neonforge-public.ps1` to publish the packages to **Nuget.org**.

6. Check in the project with a comment like: **NUGET RELEASE: 5.0.1.0**
