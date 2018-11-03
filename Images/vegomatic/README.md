# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional *-dirty* suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`.

From time-to-time you may see images tagged like `:BRANCH-*` where *BRANCH* identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

This image is a catch-all for non-production tests and utilities.  Most hives will never deploy this.

See the `vegomatic` project in the source code for more information.
