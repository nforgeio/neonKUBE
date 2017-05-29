# -----------------------------------------------------------------------------
# Java Setup
#
# NOTE: This script must be run under sudo.

if [ ! -f $SETUP_DIR/configured/java ]; then

    echo
    echo "**********************************************"
    echo "** Installing Java                          **"
    echo "**********************************************"

	add-apt-repository -y ppa:webupd8team/java
	apt-get -q update

	# Need to do this to avoid the interactive license agreement.

	echo debconf shared/accepted-oracle-license-v1-1 select true | debconf-set-selections
	echo debconf shared/accepted-oracle-license-v1-1 seen true | debconf-set-selections

	if [ "$JAVA_VERSION" = "8" ]; then
		apt-get -q -y install oracle-java8-installer
	else
		apt-get -q -y install oracle-java7-installer
	fi

	java -version

    # Indicate that we've successfully installed Java.

    echo CONFIGURED > $SETUP_DIR/configured/java

fi

