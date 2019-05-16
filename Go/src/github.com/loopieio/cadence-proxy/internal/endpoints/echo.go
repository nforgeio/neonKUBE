package endpoints

import (
	"fmt"
	"net/http"

	"github.com/loopieio/cadence-proxy/internal/messages"
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

	// read the body and deserialize it
	message, err := readAndDeserialize(r.Body)
	if err != nil {

		// write the error and status code into response
		http.Error(w, err.Error(), http.StatusBadRequest)
		panic(err)
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug(fmt.Sprintf("Echo message type %d", int(message.GetProxyMessage().Type)))

	// serialize the message
	serializedMessageCopy, err := cloneForEcho(message)
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

func cloneForEcho(message messages.IProxyMessage) (b []byte, e error) {

	// recover from panic
	defer func() {
		if r := recover(); r != nil {

			// $debug(jack.burns): DELETE THIS!
			logger.Debug("Recovered in cloneForEcho")
			e = fmt.Errorf("panic %v", r)
			b = nil
		}
	}()

	// create a clone of the message to send back
	// as a PUT request
	messageCopy := message.Clone()
	proxyMessage := messageCopy.GetProxyMessage()

	// serialize the cloned message into a []byte
	// to send back over the network
	serializedMessageCopy, err := proxyMessage.Serialize(false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error serializing proxy message", zap.Error(err))
		return nil, err
	}

	return serializedMessageCopy, nil

}
