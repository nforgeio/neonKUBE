# Image Tags

GOLANG images are tagged with the language release version plus the image build date and the latest production image will be tagged with `:latest`.

# Description

This image is intended for developing GOLANG based applications:

* Ubuntu-16.04 based image with GOLANG installed
* Assumes that GOLANG source code is mapped in at `/src` where `PROJECT` is the go project name.
* Container sets the current directory to `/src/PROJECT` before running the command.
* Simply pass `go` commands, like:

&nbsp;&nbsp;&nbsp;&nbsp;`docker run --rm -v PROJECT-PATH:/src nhive/golang PROJECT go build`

# Build Outputs

The `go build` command generates an executable named PROJECT within the `PROJECT` directory by default.  This is inconvenient when using source control because we don't want to check in build outputs.  This script works around this:
 
If `COMMAND` completes with `exitcode=0` and a `src/PROJECT/PROJECT` file exists afterwards, then this file will be moved to the `bin` subdirectory (creating the directory if necessary).  This works well for environments where `bin` folders have been added to `.gitignore`.
