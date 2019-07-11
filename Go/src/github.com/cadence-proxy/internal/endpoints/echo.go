//-----------------------------------------------------------------------------
// FILE:		echo.go
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

	"go.uber.org/zap"

	"github.com/cadence-proxy/internal/messages"
)

// EchoHandler is the handler function for the /echo endpoint used for testing serialization
// and deserialization of ProxyMessages that are sent
// via HTTP PUT over the network.
//
// param w http.ResponseWriter
//
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
	logger.Debug(fmt.Sprintf("Echo message type %s", message.GetProxyMessage().Type.String()))

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
