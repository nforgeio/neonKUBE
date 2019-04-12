package endpoints

import (
	"io/ioutil"
	"log"
	"net/http"

	"github.com/go-chi/chi"
	"github.com/loopieio/go-cadence-proxy/cmd/cadenceproxy/common"
)

// CadenceRouter holds http request methods
// for cadence requests and returns an http.Handler
// to be mounted to the primary route tree
//
// Returns http.Handler -> handler to be mounted to the
// main route tree
func CadenceRouter() http.Handler {

	// new chi router to handle endpoints
	router := chi.NewRouter()

	// HTTP methods
	router.Post("/", PayloadHandler)
	return router
}

// PayloadHandler accepts a []byte as a payload
// Then converts it into an Operation object
// HTTP POST
func PayloadHandler(w http.ResponseWriter, r *http.Request) {

	// new Operation object
	var op common.Operation

	// Check if there is a request body
	// If there is then parse it and try to decode it into an Operation object
	if r.Body != nil {

		// new []byte to hold the encoded payload
		var payload []byte

		// Read the request body into a []byte
		payload, err := ioutil.ReadAll(r.Body)
		if err != nil {
			log.Panicln("Error reading request body: ", err)
		}

		//r.Body = ioutil.NopCloser(bytes.NewBuffer(payload))

		// decode the []byte request body into an Operation object
		op = common.ByteSliceToOperation(payload)

		// Log a pretty printed out Operation from the
		// Passed []byte
		op.OperationToString()

		// Encode back to a []byte
		// Write it as a response to the request
		b := common.OperationToByteSlice(op)
		w.Write(b)

	} else {
		log.Panicln("No content in request body")
	}
}

// ConfigureCadenceClientHelper takes an Operation and
// creates a new cadence Helper instance.  The Helper is used to make
// calls to the cadence client and holds all of the configurations
// for actually building a cadence client instance, building a
// cadence domain client, and configuring the TChannels on which calls
// to the cadence server from the client will be made
//
// Param args map[string]*string -> the operation arguments
// holding the configuration data to used to initialize the
// cadence common.Helper and construct the cadence client
// and the cadence domain client
//
// Returns *common.Helper -> a pointer to a new common.Helper which can be used
// to make cadence client calls to the cadence server
// Returns err -> any errors that might be thrown during the initialization of cadence
// Helper
func ConfigureCadenceClientHelper(args map[string]*string) (*common.Helper, error) {

	var h *common.Helper
	var err error

	return h, err

}
