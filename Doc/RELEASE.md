# neonKUBE Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

2. Merge **PROD** into **MASTER** to ensure that we have any stray changes from there too.

3. Manually clean and rebuild the entire solution: RELEASE configuration.

4. Make sure that the `neon-cli` image is rebuilt with the correct version and is pushed to DockerHub.

5. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

6. Deploy a test cluster.

7. Run all unit tests against the test cluster and fix any bugs until all tests pass.

## Release 

1. Select the **PROD** branch.  Merge from **MASTER**.

2. Open the **Properties** each of the library projects and update the **Release notes**.

3. Update `$/nuget-version.txt` (or `GitHub/nuget-version.txt` in the solution) with the 
   new package version.

4. Manually clean and rebuild the entire solution: RELEASE configuration.

5. Ensure that the `neon-cli` image is rebuilt with the correct version and is pushed to DockerHub.

6. Execute **as ADMIN**: `powershell -f %NF_ROOT%/Toolbin/nuget-neonforge-public.ps1` to publish the packages to **NuGet.org**.

7. Commit all changes with a comment like: **RELEASE: 1.0.0+1901** but **DO NOT** push to GitHub yet.

8. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

9. Upgrade an older cluster and verify by running cluster unit tests.

10. Deploy a fresh cluster and verify by running the cluster unit tests.

11. Fix any important issues and commit the changes.

12. Push the **PROD** branch to GitHub.

13. Create a new Git branch from PROD named for the release (like **release-1.0.0+1901**) and push to GitHub.

## Post Release

1. Check out the **MASTER** branch and merge changes from **PROD**.

2. Bump the `neon-cli` version in `Program.cs`.

3. Merge the changes into the **MASTER** branch and rebuild the solution.

4. Merge **MASTER** into the **JEFF** and/or any other development brances and rebuild all of those images, as required.

5. Start the next release notes document.

 # Release Version Conventions

* Use semantic versioning.
* The MAJOR, MINOR, and PATCH versions work as defined: [here](https://semver.org/)
* Patch versions start at 0.
* The release year and month is encoded as build metadata as YYMM as in: **1.0.0+1901**
* Intermediate development releases will use versions like: **MAJOR.MINOR.PATCH+1901-alpha-#** where **YYMM* specifies the scheduled month for the release and **#** starts at **0** and is incremented for every development release made since the last stable release.  Intermediate releases are not generally intended for public consumption.
* If a Stable release slips passed the scheduled release month, we'll retain the old month for up to 15 days into the next month.  Past that, we'll update **YYMM** to the actual published month.
* Non-production releases that are stable enough for limited public consumption will look like: **MAJOR.MINOR.PATCH+1901-preview-#**
* Near production release candidates will look like: **MAJOR.MINOR.PATCH+1901-rc-#**
