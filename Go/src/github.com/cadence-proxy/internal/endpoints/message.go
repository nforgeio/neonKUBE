//-----------------------------------------------------------------------------
// FILE:		message.go
// CONTRIBUTOR: John C Burns
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

package endpoints

import (
	"fmt"
	"net/http"

	"github.com/cadence-proxy/internal/messages"
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
		return
	}

	// read and deserialize the body
	message, err := readAndDeserialize(r.Body)
	if err != nil {

		// write the error and status code into response
		http.Error(w, err.Error(), http.StatusBadRequest)
		return
	}

	// make channel for writing a response to the sender
	// go routine to process the message
	// receive error on responseChan
	responseChan := make(chan error)
	go proccessIncomingMessage(message, responseChan)

	// block and wait for error value
	// on responseChan to send an http.Response
	// back to the Neon.Cadence Lib
	err = <-responseChan
	if err != nil {

		// write the error and status code into response
		http.Error(w, err.Error(), http.StatusBadRequest)
		return
	}

	// write the response header to 200 OK
	w.WriteHeader(http.StatusOK)
}

// -------------------------------------------------------------------------
// Helper methods for handling incoming messages

func proccessIncomingMessage(message messages.IProxyMessage, responseChan chan error) {

	// defer the termination of the server
	// and the closing of the responseChan
	defer func() {

		// close the responseChan
		// check to see if terminate is true, if it is then gracefully
		// shut down the server instance by sending a truth bool value
		// to the instance's ShutdownChannel
		close(responseChan)
		if terminate {
			Instance.ShutdownChannel <- true
		}
	}()

	// switch on message interface type (IProxyRequest/IProxyReply)
	var err error
	switch s := message.(type) {
	case nil:

		// $debug(jack.burns): DELETE THIS!
		err = fmt.Errorf("nil type for incoming ProxyMessage of type %v", message.GetType())
		logger.Debug("Error processing incoming message", zap.Error(err))
		responseChan <- err

	// IProxyRequest
	case messages.IProxyRequest:
		responseChan <- nil
		err = handleIProxyRequest(s)

	// IProxyReply
	case messages.IProxyReply:
		responseChan <- nil
		err = handleIProxyReply(s)

	// Unrecognized type
	default:

		// $debug(jack.burns): DELETE THIS!
		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %v", message.GetType())
		logger.Debug("Error processing incoming message", zap.Error(err))
		responseChan <- err
	}

	// there should not be error values in here
	// if there are, then something is wrong with the server
	// and most likely needs to be terminated
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Error("Error Handling ProxyMessage", zap.Error(err))
	}
}
