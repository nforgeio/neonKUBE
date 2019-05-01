package endpoints

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"net/http"
	"os"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/initialize"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"

	cadenceclient "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceclient"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	"go.uber.org/zap"
)

const (

	// contentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	_contentType = "application/x-neon-cadence-proxy"
)

// ReplyAddress specifies the address that the Neon.Cadence library
// will be listening on for replies from the cadence proxy
var _replyAddress string

// ProxyMessageHandler accepts a []byte as a payload
// Then converts it into an ProxyMessage object
// HTTP PUT
//
// param w http.ResponseWriter
// param r *http.Request
func ProxyMessageHandler(w http.ResponseWriter, r *http.Request) {

	// new global logger
	logger := zap.L()

	// log when a new request has come in
	logger.Info("Request Recieved",
		zap.String("Address", fmt.Sprintf("http://%s%s", r.Host, r.URL.String())),
		zap.String("Method", r.Method),
		zap.Int("ProccessId", os.Getpid()))

	// check if the content type is correct
	if r.Header.Get("Content-Type") != _contentType {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Incorrect Content-Type",
			zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", _contentType))

		// write the error to and status code into response
		errStr := fmt.Sprintf("Incorrect Content-Type %s. Content must be %s", r.Header.Get("Content-Type"), _contentType)
		w.WriteHeader(http.StatusBadRequest)
		w.Write([]byte(errStr + ".  "))

	} else if r.Method != http.MethodPut {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Invalid HTTP Method",
			zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut))

		// write the error and status code into the reponse
		errStr := fmt.Sprintf("Invalid HTTP Method: %s, must be HTTP Metho: %s", r.Method, http.MethodPut)
		w.WriteHeader(http.StatusMethodNotAllowed)
		w.Write([]byte(errStr + ".  "))

	} else {

		// create an empty []byte and read the
		// request body into it if not nil
		var payload []byte
		payload, err := ioutil.ReadAll(r.Body)
		if err != nil {

			// $debug(jack.burns): DELETE THIS!
			logger.Debug("Null request body", zap.String("Error", err.Error()))

			// write the error and status code into response
			w.WriteHeader(http.StatusInternalServerError)
			w.Write([]byte(err.Error() + ".  "))
			panic(err)
		}

		// deserialize the payload
		buf := bytes.NewBuffer(payload)
		message, err := base.Deserialize(buf)
		if err != nil {

			// $debug(jack.burns): DELETE THIS!
			logger.Debug("Error deserializing input", zap.String("Error", err.Error()))

			// write the error and status code into response
			w.WriteHeader(http.StatusInternalServerError)
			w.Write([]byte(err.Error() + ".  "))
			panic(err)
		}

		// get the proxy message from the IProxyMessage
		proxyMessage := message.GetProxyMessage()

		// if initialization request then extract the library address
		// and library port from the properties
		if proxyMessage.Type == messages.InitializeRequest {
			var initailizeRequestIProxyMessage base.IProxyMessage = message
			v, ok := initailizeRequestIProxyMessage.(*initialize.InitializeRequest)
			if ok {
				//_replyAddress = fmt.Sprintf("http://%s:%s/", *v.GetLibraryAddress(), *v.GetLibraryPort())
				_replyAddress = "http://127.0.0.2:5001/"

				// $debug(jack.burns): DELETE THIS!
				logger.Debug("InitializeRequest info",
					zap.String("Library Address", *v.GetLibraryAddress()),
					zap.String("LibaryPort", *v.GetLibraryPort()),
					zap.String("Reply Address", _replyAddress))
			}
		}

		// serialize the cloned message into a []byte
		// to send back over the network
		var serializedMessageCopy []byte
		serializedMessageCopy, err = proxyMessage.Serialize()
		if err != nil {

			// $debug(jack.burns): DELETE THIS!
			logger.Debug("Error serializing proxy message", zap.String("Error", err.Error()))

			// write the error and status code into response
			w.WriteHeader(http.StatusInternalServerError)
			w.Write([]byte(err.Error() + ".  "))
			panic(err)
		}

		resp, err := putRequest(serializedMessageCopy, _replyAddress)
		if err != nil {

			// write the error and status code into response
			w.WriteHeader(http.StatusInternalServerError)
			w.Write([]byte(err.Error() + ".  "))
			panic(err)
		}
		defer resp.Body.Close()

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Neon.Cadence Library Response",
			zap.String("Response Status", resp.Status),
			zap.String("Request URL", resp.Request.URL.String()))
	}
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

	// log when a new request has come in
	logger.Info("Request Recieved", zap.String("Address", fmt.Sprintf("http://%s%s", r.Host, r.URL.String())),
		zap.String("Method", r.Method),
		zap.Int("ProccessId", os.Getpid()))

	// check if the content type is correct
	if r.Header.Get("Content-Type") != _contentType {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Incorrect Content-Type", zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", _contentType))

		// write the error to and status code into response
		errStr := fmt.Sprintf("Incorrect Content-Type %s. Content must be %s", r.Header.Get("Content-Type"), _contentType)
		w.WriteHeader(http.StatusBadRequest)
		w.Write([]byte(errStr + ".  "))

	} else if r.Method != http.MethodPut {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Invalid HTTP Method", zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut))

		// write the error and status code into the reponse
		errStr := fmt.Sprintf("Invalid HTTP Method: %s, must be HTTP Metho: %s", r.Method, http.MethodPut)
		w.WriteHeader(http.StatusMethodNotAllowed)
		w.Write([]byte(errStr + ".  "))

	} else {

		// create an empty []byte and read the
		// request body into it if not nil
		var payload []byte
		payload, err := ioutil.ReadAll(r.Body)
		if err != nil {

			// $debug(jack.burns): DELETE THIS!
			logger.Debug("Null request body", zap.String("Error", err.Error()))

			// write the error and status code into response
			w.WriteHeader(http.StatusInternalServerError)
			w.Write([]byte(err.Error() + ".  "))
			panic(err)
		}

		serializedMessageCopy, err := deserializeCloneAndSerializeMessage(payload)
		if err != nil {

			// write the error and status code into response
			w.WriteHeader(http.StatusInternalServerError)
			w.Write([]byte(err.Error() + ".  "))
			panic(err)
		}

		// respond with the serialize message copy
		w.Write(serializedMessageCopy)
	}
}

func putRequest(content []byte, address string) (*http.Response, error) {

	// get the global logger
	logger := zap.L()

	// create a buffer with the serialized bytes to reply with
	// and create the PUT request
	buf := bytes.NewBuffer(content)
	req, err := http.NewRequest(http.MethodPut, _replyAddress, buf)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error creating Neon.Cadence Library request", zap.String("Error", err.Error()))
		return nil, err
	}

	// set the request header to specified content type
	req.Header.Set("Content-Type", _contentType)

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Neon.Cadence Library request",
		zap.String("Request Address", req.URL.String()),
		zap.String("Request Content-Type", req.Header.Get("Content-Type")),
		zap.String("Request Method", req.Method))

	// initialize the http.Client and send the request
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error sending Neon.Cadence Library request", zap.String("Error", err.Error()))
		return nil, err
	}

	return resp, nil
}

func deserializeCloneAndSerializeMessage(content []byte) ([]byte, error) {

	// set logger to global logger
	logger := zap.L()

	// deserialize the payload
	buf := bytes.NewBuffer(content)
	message, err := base.Deserialize(buf)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error deserializing input", zap.String("Error", err.Error()))
		return nil, err
	}

	// create a clone of the message to send back
	// as a PUT request
	messageCopy := message.Clone()
	proxyMessage := messageCopy.GetProxyMessage()

	// serialize the cloned message into a []byte
	// to send back over the network
	var serializedMessageCopy []byte
	serializedMessageCopy, err = proxyMessage.Serialize()
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error serializing proxy message", zap.String("Error", err.Error()))
		return nil, err
	}

	return serializedMessageCopy, nil

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
