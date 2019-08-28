# -----------------------------------------------------------------------------
# Setup .NET Core and ASP.NET 5
#
# The original instructions are here: http://docs.asp.net/en/latest/getting-started/installing-on-linux.html
#
# NOTE: This script must be run under sudo.
#
# NOTE: macros of the form $(...) will be replaced by the deployer.

if [ ! -f $SETUP_DIR/configured/dotnet ]; then

    echo
    echo "**********************************************"
    echo "** Installing .NET Core and ASP.NET 5       **"
    echo "**********************************************"

	# Download and install the .NET Execution Environment (DNX)

	curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh

	# Install the DNX prerequisites

	apt-get -q -y install libunwind8 gettext libssl-dev libcurl3-dev zlib1g libicu-dev

	# Use DNVM to install DNX for .NET Core.

	dnvm upgrade -r coreclr

    # Indicate that we've successfully configured .NET.

    echo CONFIGURED > $SETUP_DIR/configured/dotnet

fi
