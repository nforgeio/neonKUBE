# neonHIVE Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

2. Merge **PROD** into **MASTER** to ensure that we have any stray changes from there too.

3. Manually clean and rebuild the entire solution: RELEASE configuration.

4. Make sure that the `neon-cli` image is rebuilt with the correct version and is pushed to DockerHub.

5. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

6. Deploy a test hive.

7. Run all unit tests against the test hive and fix any bugs until all tests pass.

## Release 

1. Select the **PROD** branch.  Merge from **MASTER**.

2. Open the **Properties** each of the library projects and update the **Release notes**.

3. Update `$/nuget-version.txt` (or `GitHub/nuget-version.txt` in the solution) with the 
   new package version.

4. Manually clean and rebuild the entire solution: RELEASE configuration.

5. Ensure that the `neon-cli` image is rebuilt with the correct version and is pushed to DockerHub.

6. Execute **as ADMIN**: `powershell -f %NF_ROOT%/Toolbin/nuget-neonforge-public.ps1` to publish the packages to **NuGet.org**.

7. Commit all changes with a comment like: **RELEASE: 18.10.0-alpha.4** but **DO NOT** push to GitHub yet.

8. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

9. Upgrade an older hive and verify by running Hive unit tests.

10. Deploy a fresh hive and verify by running the hive unit tests.

11. Fix any important issues and commit the changes.

12. Push the **PROD** branch to GitHub.

13. Create a new Git branch from PROD named for the release (like **release-18.10.0-alpha.4**) and push to GitHub.

## Post Release

1. Check out the **MASTER** branch and merge changes from **PROD**.

2. Bump the `neon-cli` version in `Program.cs`.

3. Merge the changes into the **MASTER** branch and rebuild the solution.

4. Merge **MASTER** into the **JEFF** and/or any other development brances and rebuild all of those images, as required.

5. Start the next release notes document.

 # Release Version Conventions

* Stable, Edge and and LTS releases will continue to use the **YY.M.PATCH** convention where the month.
* Patch releases are guaranteed to be backwards compatible.
* Releases where one or both of YY or M were advanced may not be backwards compatible but we'll try very hard to avoid these issues or provide an upgrade path.
* The month field **will not** include a leading **""0""** due to NuGet issues.
* Intermediate development releases will use versions like: **YY.M.0-alpha.N** where **YY.M* specifies the actual date for the release and **N** starts at **0** and is incremented for every development release made since the Stable, Edge, or LTS release.  Intermediate releases are not generally intended for public consumption.
* Intermediate public releases are called previews.  There are types of preview release:
  * Preview of a patch release for an existing release.  This will look like **YY.M.PATCH-preview-N** where **PATCH** is the scheduled patch number and **N** starts at **0** and is incremented for every preview release for the patch.
  * Preview of an upcoming Stable, Edge, or LTS release.  These release versions will look like **YY.M.0-preview-B** where **YY.M** is the expected release month.
* If a Stable, Edge, or LTS release slips passed the scheduled release month, we'll retain the old month for up to 15 days into the next month.  Past that, we'll update **YY.M** to the actual published month.
