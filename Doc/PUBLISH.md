# Steps to publish this project:

*Note that these steps depend on how my personal (Jeff Lill's) workstation is configured.*

1. Set the `NF_NUGET_API_KEY` environment variable to the Nuget API key.

2. Open the **Properties** each of the library projects and update the **Release notes**.

3. Update `$/nuget-version.txt` (or `GitHub/nuget-version.txt` in the solution) with the 
   new package version.

4. Manually rebuild the entire solution.

5. Execute `$/Toolbin/nuget-neonforge-public.cmd` to publish the packages to **Nuget.org**.

6. Check in the project with a comment like: **RELEASED: 5.0.1.0**
