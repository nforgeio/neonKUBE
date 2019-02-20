# neonKUBE Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

2. Update the product versions as required: 

  `Neon.Global.Build.NuGetVersion` - Version for the libraries published to **nuget.org**
  `Neon.Global.Build.ProductVersion` - Version for the released products and installers

3. Manually clean and rebuild the entire solution (**RELEASE** configuration**): 

  * Delete the contents of the **$\Build** folder.
  * Ensure that this environment variable is set: **NF_PUBLISH_BINARIES=1**
  * Clean the **RELEASE** configuration.
  * Build the **RELEASE** configuration.

4. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

5. Deploy a test cluster.

6. Run all unit tests against the test cluster and fix any bugs until all tests pass.

## Release 

1. Update `$/nuget-version.txt` (or `GitHub/nuget-version.txt` in the solution) with the 
   new package version as required.

2. Update `$/kube-version.txt` (or `GitHub/kube-version.txt` in the solution) with the 
   required Kubernetes version as required.

3. Open the **Properties** each of the library projects and update the **Release notes**.

4. Create a new local `release-VERSION` branch from `MASTER` (where `VERSION` is the same version as saved to `$/nuget-version.txt`).

5. Manually clean and rebuild the entire solution: RELEASE configuration.

7. Execute **as ADMIN**: `powershell -f %NF_ROOT%/Toolbin/nuget-neonforge-public.ps1` to publish the packages to **nuget.org**.

8. Commit all changes with a comment like: **RELEASE: 1.0.0+1901** but **DO NOT** push to GitHub yet.

9. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

10. Upgrade an older cluster and verify by running cluster unit tests.

11. Deploy a fresh cluster and verify by running the cluster unit tests.

12. Fix any important issues and commit the changes.

13. Push the `release-VERSION` branch to GitHub.

14. Go back to the `MASTER` branch and merge any changes from the `release-VERSION` branch.

15. GitHub Release: [link](https://help.github.com/articles/creating-releases/)

  a. Create the release if it doesn't already exist
  b. Set **Tag** to the version
  c. Set **Target** to the `release-VERSION` branch
  d: Check **This is a pre-release** as required
  e. Add the release setup binary named like: **neonKUBE-setup-0.1.0+1902-alpha-0.exe**
  f. Edit the release notes including adding the SH512 for the binaries:

    `cat <binary-file> | openssl dgst -sha512`

  g. Publish the release

## Post Release

1. Bump the version numbers in `$/nuget-version.txt` and `$Lib/Neon.Global/Build.cs`.

2. Merge **MASTER** into the **JEFF** and/or any other development branches, as required.

3. Create a draft for the next GitHub release.

 # Release Version Conventions

* Use semantic versioning.
* The MAJOR, MINOR, and PATCH versions work as defined: [here](https://semver.org/)
* Patch versions start at 0.
* The release year and month is encoded as build metadata as YYMM as in: **1.0.0+1901**
* Intermediate development releases will use versions like: **MAJOR.MINOR.PATCH+1901-alpha-#** where **YYMM* specifies the scheduled month for the release and **#** starts at **0** and is incremented for every development release made since the last stable release.  Intermediate releases are not generally intended for public consumption.
* If a Stable release slips passed the scheduled release month, we'll retain the old month for up to 15 days into the next month.  Past that, we'll update **YYMM** to the actual published month.
* Non-production releases that are stable enough for limited public consumption will look like: **MAJOR.MINOR.PATCH+1901-preview-#**
* Near production release candidates will look like: **MAJOR.MINOR.PATCH+1901-rc-#**
