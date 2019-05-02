package endpoints

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"net/http"
	"os"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	"go.uber.org/zap"
)

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
		zap.Int("ProccessId", os.Getpid()),
	)

	// check if the content type is correct
	if r.Header.Get("Content-Type") != ContentType {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Incorrect Content-Type",
			zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", ContentType),
		)

		// write the error to and status code into response
		err := fmt.Errorf("incorrect Content-Type %s. Content must be %s",
			r.Header.Get("Content-Type"),
			ContentType,
		)
		http.Error(w, err.Error(), http.StatusBadRequest)
		return

	}

	if r.Method != http.MethodPut {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Invalid HTTP Method",
			zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut),
		)

		// write the error and status code into the reponse
		err := fmt.Errorf("invalid HTTP Method: %s, must be HTTP Metho: %s",
			r.Method,
			http.MethodPut,
		)
		http.Error(w, err.Error(), http.StatusMethodNotAllowed)
		return

	}

	// create an empty []byte and read the
	// request body into it if not nil
	var payload []byte
	payload, err := ioutil.ReadAll(r.Body)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Null request body", zap.String("Error", err.Error()))

		// write the error and status code into response
		http.Error(w, err.Error(), http.StatusBadRequest)
		panic(err)
	}

	serializedMessageCopy, err := cloneForEcho(payload)
	if err != nil {

		// write the error and status code into response
		http.Error(w, err.Error(), http.StatusBadRequest)
		panic(err)
	}

	// respond with the serialize message copy
	w.Write(serializedMessageCopy)
}

func cloneForEcho(content []byte) ([]byte, error) {

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
	serializedMessageCopy, err := proxyMessage.Serialize()
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error serializing proxy message", zap.String("Error", err.Error()))
		return nil, err
	}

	return serializedMessageCopy, nil

}
