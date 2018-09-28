# Steps to publish the project NuGet packages:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

2. Merge **PROD** into **MASTER** to ensure that we have any stray changes from there too.

3. Manually clean and rebuild the entire solution: RELEASE configuration.

4. Make sure that the `neon-cli` image is rebuilt with the correct version and is pushed to DockerHub.

5. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

6. Deploy a test hive.

7. Run all unit tests against the test hive and fix any bugs until all tests pass.

## Publish 

1. Select the **PROD** branch.  Merge from **MASTER**.

2. Open the **Properties** each of the library projects and update the **Release notes**.

3. Update `$/nuget-version.txt` (or `GitHub/nuget-version.txt` in the solution) with the 
   new package version.

4. Manually clean and rebuild the entire solution: RELEASE configuration.

5. Make sure that the `neon-cli` image is rebuilt with the correct version and is pushed to DockerHub.

6. Execute `$/Toolbin/nuget-neonforge-public.ps1` to publish the packages to **NuGet.org**.

7. Commit all changes with a comment like: **RELEASE: 18.9.3-alpha** and push to GutHub.

8. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

9. Create a new Git branch from PROD named for the release (like **release-18.9.3-alpha**) and push to GitHub.

10. Merge the changes into the **MASTER** branch and rebuild all of those images as well.

11. Merge the changes into the **JEFF** and/or any other development brances and rebuild all of those images, as required.
