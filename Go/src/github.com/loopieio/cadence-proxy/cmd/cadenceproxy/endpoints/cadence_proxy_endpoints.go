package endpoints

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"net/http"
	"os"

	cadenceclient "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceclient"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	"go.uber.org/zap"
)

const (

	// contentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	_contentType = "application/x-neon-cadence-proxy"
)

// ProxyMessageHandler accepts a []byte as a payload
// Then converts it into an ProxyMessage object
// HTTP PUT
//
// param w http.ResponseWriter
// param r *http.Request
func ProxyMessageHandler(w http.ResponseWriter, r *http.Request) {
	// $debug(jack.burns): DELETE THIS!
}

// EchoHandler is the handler function for the /echo endpoint used for testing serialization
// and deserialization of ProxyMessages that are sent
// via HTTP PUT over the network.
//
// param w http.ResponseWriter
// param r *http.Request
func EchoHandler(w http.ResponseWriter, r *http.Request) {

	// new global logger
	logger := zap.L()

	// construct the request address
	requestAddress := fmt.Sprintf("%s%s", r.Host, r.URL.String())

	// log when a new request has come in
	logger.Info("Request Recieved", zap.String("Address", requestAddress), zap.String("Method", r.Method), zap.Int("ProccessId", os.Getpid()))

	if r.Header.Get("Content-Type") != _contentType {
		errStr := fmt.Sprintf("Incorrect Content-Type %s. Content must be %s", r.Header.Get("Content-Type"), _contentType)
		w.WriteHeader(http.StatusBadRequest)
		w.Write([]byte(errStr + ".  "))
		return
	}

	if r.Method != http.MethodPut {
		errStr := fmt.Sprintf("Invalid HTTP Method: %s, must be HTTP Metho: %s", r.Method, http.MethodPut)
		w.WriteHeader(http.StatusMethodNotAllowed)
		w.Write([]byte(errStr + ".  "))
		return
	}

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
	req, err := http.NewRequest(http.MethodPut, requestAddress, buf)
	req.Header.Set("Content-Type", _contentType)
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
func ConfigureCadenceClientHelper(args map[string]*string) (*cadenceclient.CadenceClientHelper, error) {

	var h *cadenceclient.CadenceClientHelper
	var err error

	return h, err

}
