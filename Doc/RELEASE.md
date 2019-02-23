# neonKUBE Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

2. Update the product versions as required: 

  `Neon.Global.Build.NuGetVersion` - Version for the libraries published to **nuget.org**
  `Neon.Global.Build.ProductVersion` - Version for the released products and installers

3. Manually clean and rebuild the entire solution (**RELEASE** configuration**): 

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neonkube-build `

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

5. Rebuild the RELEASE version via:

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neonkube-build -release -installer`

7. Execute **as ADMIN**: `powershell -f %NF_ROOT%/Toolbin/nuget-neonforge-public.ps1` to publish the packages to **nuget.org**.

8. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

9. Upgrade an older cluster and verify by running cluster unit tests.

10. Deploy a fresh cluster and verify by running the cluster unit tests.

11. Fix any important issues and commit the changes.

12. Push the `release-VERSION` branch to GitHub.

13. GitHub Release: [link](https://help.github.com/articles/creating-releases/)

  a. Create the release if it doesn't already exist
  b. Set **Tag** to the version
  c. Set **Target** to the `release-VERSION` branch
  e: Check **This is a pre-release** as required
  f. Add the release setup binary named like: **neonKUBE-setup-0.1.0+1902-alpha-0.exe**
  g. Edit the release notes including adding the SH512 for the setup from:

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"$NF_BUILD%\neonKUBE-setup.exe.sha512`

  g. Publish the release

## Post Release

1. Merge any changes from the RELEASE branch back into MASTER.

2. Bump the version numbers in `$/nuget-version.txt` and `$Lib/Neon.Global/Build.cs`.

3. Merge **MASTER** into the **JEFF** and/or any other development branches, as required.

4. Create a draft for the next GitHub release.

 # Release Version Conventions

* Use semantic versioning.
* The MAJOR, MINOR, and PATCH versions work as defined: [here](https://semver.org/)
* Patch versions start at 0.
* The release year and month is encoded as build metadata as YYMM as in: **1.0.0+1901**
* Intermediate development releases will use versions like: **MAJOR.MINOR.PATCH+1901-alpha-#** where **YYMM* specifies the scheduled month for the release and **#** starts at **0** and is incremented for every development release made since the last stable release.  Intermediate releases are not generally intended for public consumption.
* If a Stable release slips passed the scheduled release month, we'll retain the old month for up to 15 days into the next month.  Past that, we'll update **YYMM** to the actual published month.
* Non-production releases that are stable enough for limited public consumption will look like: **MAJOR.MINOR.PATCH+1901-preview-#**
* Near production release candidates will look like: **MAJOR.MINOR.PATCH+1901-rc-#**
