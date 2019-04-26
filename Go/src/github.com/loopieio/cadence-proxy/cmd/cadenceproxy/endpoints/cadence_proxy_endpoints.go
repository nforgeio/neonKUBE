package endpoints

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"net/http"

	cadenceclient "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceclient"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

const (

	// ContentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	contentType = "application/x-neon-cadence-proxy"
)

// ProxyMessageHandler accepts a []byte as a payload
// Then converts it into an ProxyMessage object
// HTTP POST
func ProxyMessageHandler(w http.ResponseWriter, r *http.Request) {

	// // new ProxyMessage object
	// var pm base.ProxyMessage

	// // Check if there is a request body
	// // If there is then parse it and try to decode it into an ProxyMessage object
	// if r.Body != nil {

	// 	// new []byte to hold the encoded payload
	// 	var payload []byte

	// 	// Read the request body into a []byte
	// 	payload, err := ioutil.ReadAll(r.Body)
	// 	if err != nil {
	// 		log.Panicln("Error reading request body: ", err)
	// 	}

	// 	buf := bytes.NewBuffer(payload)

	// 	// decode the []byte request body into an ProxyMessage object
	// 	pm = base.Deserialize(buf, false)

	// 	// Log a pretty printed out ProxyMessage from the
	// 	// Passed []byte
	// 	pm.String()

	// 	// Encode back to a []byte
	// 	// Write it as a response to the request
	// 	b := pm.Serialize()
	// 	w.Write(b)

	// } else {
	// 	log.Panicln("No content in request body")
	// }
}

// EchoHandler is the handler function for the /echo endpoint used for testing serialization
// and deserialization of ProxyMessages that are sent over the network.
func EchoHandler(w http.ResponseWriter, r *http.Request) {

	if r.Header.Get("Content-Type") != contentType {
		defer r.Body.Close()
		errStr := fmt.Sprintf("Incorrect Content-Type %s. Content must be %s", r.Header.Get("Content-Type"), contentType)
		w.WriteHeader(http.StatusBadRequest)
		w.Write([]byte(errStr + ".  "))
		return
	}

	if r.Body == nil {
		defer r.Body.Close()
		errStr := "Cannot parse null request body"
		w.WriteHeader(http.StatusBadRequest)
		w.Write([]byte(errStr + ".  "))
		return
	}

	if r.Method != http.MethodPost {
		defer r.Body.Close()
		errStr := fmt.Sprintf("Invalid HTTP Method: %s, must be HTTP Metho: %s", r.Method, http.MethodPost)
		w.WriteHeader(http.StatusMethodNotAllowed)
		w.Write([]byte(errStr + ".  "))
		return
	}

	defer r.Body.Close()

	var payload []byte
	payload, err := ioutil.ReadAll(r.Body)
	if err != nil {
		w.WriteHeader(http.StatusInternalServerError)
		w.Write([]byte(err.Error() + ".  "))
		panic(err)
	}

	buf := bytes.NewBuffer(payload)
	message, err := base.Deserialize(buf)
	if err != nil {
		w.WriteHeader(http.StatusInternalServerError)
		w.Write([]byte(err.Error() + ".  "))
		panic(err)
	}

	messageCopy := message.Clone()
	proxyMessage := messageCopy.GetProxyMessage()

	var serializedMessageCopy []byte
	serializedMessageCopy, err = proxyMessage.Serialize()
	if err != nil {
		w.WriteHeader(http.StatusInternalServerError)
		w.Write([]byte(err.Error() + ".  "))
		panic(err)
	}

	buf = bytes.NewBuffer(serializedMessageCopy)
	req, err := http.NewRequest(http.MethodPost, r.RequestURI, buf)
	req.Header.Set("Content-Type", contentType)
	if err != nil {
		w.WriteHeader(http.StatusInternalServerError)
		w.Write([]byte(err.Error() + ".  "))
		panic(err)
	}

	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		w.Write([]byte(err.Error() + ".  "))
		panic(err)
	}
	defer resp.Body.Close()
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
func ConfigureCadenceClientHelper(args map[string]*string) (*cadenceclient.Helper, error) {

	var h *cadenceclient.Helper
	var err error

	return h, err

}
