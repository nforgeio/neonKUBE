package endpoints

import (
	"bytes"
	"io/ioutil"
	"log"
	"net/http"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadence"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

// ProxyMessageHandler accepts a []byte as a payload
// Then converts it into an ProxyMessage object
// HTTP POST
func ProxyMessageHandler(w http.ResponseWriter, r *http.Request) {

	// new ProxyMessage object
	var pm base.ProxyMessage

	// Check if there is a request body
	// If there is then parse it and try to decode it into an ProxyMessage object
	if r.Body != nil {

		// new []byte to hold the encoded payload
		var payload []byte

		// Read the request body into a []byte
		payload, err := ioutil.ReadAll(r.Body)
		if err != nil {
			log.Panicln("Error reading request body: ", err)
		}

		buf := bytes.NewBuffer(payload)

		// decode the []byte request body into an ProxyMessage object
		pm = base.Deserialize(buf)

		// Log a pretty printed out ProxyMessage from the
		// Passed []byte
		pm.String()

		// Encode back to a []byte
		// Write it as a response to the request
		b := pm.Serialize()
		w.Write(b)

	} else {
		log.Panicln("No content in request body")
	}
}

func EchoHandler(w http.ResponseWriter, r *http.Request) {

}

// ConfigureCadenceClientHelper takes an ProxyMessage and
// creates a new cadence Helper instance.  The Helper is used to make
// calls to the cadence client and holds all of the configurations
// for actually building a cadence client instance, building a
// cadence domain client, and configuring the TChannels on which calls
// to the cadence server from the client will be made
//
// Param args map[string]*string -> the ProxyMessage Properties
// holding the configuration data to used to initialize the
// cadence Helper and construct the cadence client
// and the cadence domain client
//
// Returns *cadence.Helper -> a pointer to a new cadence.Helper which can be used
// to make cadence client calls to the cadence server
// Returns err -> any errors that might be thrown during the initialization of cadence
// Helper
func ConfigureCadenceClientHelper(args map[string]*string) (*cadence.Helper, error) {

	var h *cadence.Helper
	var err error

	return h, err

}
