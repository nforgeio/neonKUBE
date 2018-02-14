# Image Tags

GOLANG images are tagged with the language release version plus the image build date and the latest production image will be tagged with `:latest`.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

This image is intended for developing GOLANG based applications:

* Alpine image with GOLANG installed
* Assumes that GOLANG source code is mapped in at `/src`
* Simply pass `go` commands, like:

&nbsp;&nbsp;&nbsp;&nbsp;`docker run --rm -v:SRC-PATH:/src neoncluster/golang go build`

# Additional Packages

This image also includes the following packages:

* [nano](https://www.nano-editor.org/dist/v2.6/nano.html) is a simple text editor.
