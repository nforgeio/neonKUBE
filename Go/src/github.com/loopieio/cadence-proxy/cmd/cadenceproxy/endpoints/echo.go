package endpoints

import (
	"bytes"
	"io/ioutil"
	"net/http"

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

	// grab the global logger
	logger = Instance.Logger

	// check if the request has the correct content type,
	// has a body that is not nil,
	// and is an http.PUT request
	statusCode, err := checkRequestValidity(w, r)
	if err != nil {
		http.Error(w, err.Error(), statusCode)
		panic(err)
	}

	// create an empty []byte and read the
	// request body into it if not nil
	payload, err := ioutil.ReadAll(r.Body)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!

		logger.Debug("Null request body", zap.Error(err))

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
	_, err = w.Write(serializedMessageCopy)
	if err != nil {
		panic(err)
	}
}

func cloneForEcho(content []byte) ([]byte, error) {

	// deserialize the payload
	buf := bytes.NewBuffer(content)
	message, err := base.Deserialize(buf)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error deserializing input", zap.Error(err))
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
		logger.Debug("Error serializing proxy message", zap.Error(err))
		return nil, err
	}

	return serializedMessageCopy, nil

}
