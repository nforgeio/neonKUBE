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
