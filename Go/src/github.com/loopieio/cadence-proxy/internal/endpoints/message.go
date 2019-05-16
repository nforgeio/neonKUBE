package endpoints

import (
	"fmt"
	"net/http"

	"github.com/loopieio/cadence-proxy/internal/messages"
	"go.uber.org/zap"
)

// MessageHandler accepts an http.PUT requests and parses the
// request body, converts it into an ProxyMessage object
// and talks through the uber cadence client to the cadence server,
// executing the instructions incoded in the request.
//
// param w http.ResponseWriter
//
// param r *http.Request
func MessageHandler(w http.ResponseWriter, r *http.Request) {

	// grab the logger from the server instance
	logger = Instance.Logger

	// check if the request has the correct content type
	// and is an http.PUT request
	statusCode, err := checkRequestValidity(w, r)
	if err != nil {
		http.Error(w, err.Error(), statusCode)
		panic(err)
	}

	// read and deserialize the body
	message, err := readAndDeserialize(r.Body)
	if err != nil {

		// write the error and status code into response
		http.Error(w, err.Error(), http.StatusBadRequest)
		panic(err)
	}

	// make channel for writing a response to the sender
	responseChan := make(chan error)
	go func() {

		// process the incoming payload
		err := proccessIncomingMessage(message, responseChan)
		if err != nil {

			// $debug(jack.burns): DELETE THIS!
			logger.Error("Error Handling ProxyMessage", zap.Error(err))
			deathWish = true
		}

		// check to see if terminate is true, if it is then gracefully
		// shut down the server instance by sending a truth bool value
		// to the instance's ShutdownChannel
		if terminate {
			Instance.ShutdownChannel <- true
		}
	}()

	err = <-responseChan
	if err != nil {

		// write the error and status code into response
		http.Error(w, err.Error(), http.StatusBadRequest)
		panic(err)
	}

	// write the response header to 200 OK
	w.WriteHeader(http.StatusOK)
}

// -------------------------------------------------------------------------
// Helper methods for handling incoming messages

func proccessIncomingMessage(message messages.IProxyMessage, responseChan chan error) error {

	// get type of message and switch
	typeCode := message.GetProxyMessage().Type
	switch s := message.(type) {

	// Nil type value
	case nil:
		err := fmt.Errorf("nil type for incoming ProxyMessage of type %v", typeCode)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error processing incoming message", zap.Error(err))

		responseChan <- err
		return err

	// IProxyRequest
	case messages.IProxyRequest:
		responseChan <- nil
		return handleIProxyRequest(s, typeCode)

	// IProxyReply
	case messages.IProxyReply:
		responseChan <- nil
		return handleIProxyReply(s, typeCode)

	// Unrecognized type
	default:
		err := fmt.Errorf("unhandled message type. could not complete type assertion for type %v", typeCode)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error processing incoming message", zap.Error(err))

		responseChan <- err
		return err
	}
}
