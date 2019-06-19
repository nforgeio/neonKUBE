//-----------------------------------------------------------------------------
// FILE:		common.go
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
	"bytes"
	"errors"
	"fmt"
	"io"
	"io/ioutil"
	"net/http"
	"os"
	"time"

	"go.uber.org/zap"

	"github.com/cadence-proxy/internal/cadence/cadenceactivities"
	"github.com/cadence-proxy/internal/cadence/cadenceworkers"
	"github.com/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/cadence-proxy/internal/messages"
	"github.com/cadence-proxy/internal/server"
)

const (

	// ContentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	ContentType = "application/x-neon-cadence-proxy"

	// _cadenceSystemDomain is the string name of the cadence-system domain that
	// exists on all cadence servers.  This value is used to check that a connection
	// has been established to the cadence server instance and that it is ready to
	// accept requests
	_cadenceSystemDomain = "cadence-system"
)

var (

	// logger for all endpoints to utilize
	logger *zap.Logger

	// Instance is a pointer to the server instance of the current server that the
	// cadence-proxy is listening on.  This gets set in main.go
	Instance *server.Instance

	// errConnection is the custom error that is thrown when the cadence-proxy
	// is not able to establish a connection with the cadence server
	errConnection = errors.New("CadenceConnectionError{Messages: Could not establish a connection with the cadence server.}")

	// errEntityNotExist is the custom error that is thrown when a cadence
	// entity cannot be found in the cadence server
	errEntityNotExist = errors.New("EntityNotExistsError{Message: The entity you are looking for does not exist.}")

	// argumentNullError is the custom error that is thrown when trying to access a nil
	// value
	errArgumentNil = errors.New("ArgumentNilError{Message: failed to access nil value.}")

	// replyAddress specifies the address that the Neon.Cadence library
	// will be listening on for replies from the cadence proxy
	replyAddress string

	// terminate is a boolean that will be set after handling an incoming
	// TerminateRequest.  A true value will indicate that the server instance
	// needs to gracefully shut down after handling the request, and a false value
	// indicates the server continues to run
	terminate bool

	// DebugPrelaunched INTERNAL USE ONLY: Optionally indicates that the cadence-proxy will
	// already be running for debugging purposes.  When this is true, the
	// cadence-client be hardcoded to listen on 127.0.0.2:5001 and
	// the cadence-proxy will be assumed to be listening on 127.0.0.2:5000.
	// This defaults to false.
	DebugPrelaunched = false

	// cadenceClientTimeout specifies the amount of time in seconds a reply has to be sent after
	// a request has been received by the cadence-proxy
	cadenceClientTimeout time.Duration = time.Second * 600

	// ActivityContexts maps a int64 ContextId to the cadence
	// Activity Context passed to the cadence Activity functions.
	// The cadence-client will use contextIds to refer to specific
	// activity contexts when perfoming activity actions
	ActivityContexts = new(cadenceactivities.ActivityContextsMap)

	// Workers maps a int64 WorkerId to the cadence
	// Worker returned by the Cadence NewWorker() function.
	// This will be used to stop a worker via the
	// StopWorkerRequest.
	Workers = new(cadenceworkers.WorkersMap)

	// WorkflowContexts maps a int64 ContextId to the cadence
	// Workflow Context passed to the cadence Workflow functions.
	// The cadence-client will use contextIds to refer to specific
	// workflow ocntexts when perfoming workflow actions
	WorkflowContexts = new(cadenceworkflows.WorkflowContextsMap)

	// Operations is a map of operations used to track pending
	// cadence-client operations
	Operations = new(operationsMap)
)

//----------------------------------------------------------------------------
// ProxyMessage processing helpers

func checkRequestValidity(w http.ResponseWriter, r *http.Request) (int, error) {

	// log when a new request has come in
	logger.Info("Request Received",
		zap.String("Address", fmt.Sprintf("http://%s%s", r.Host, r.URL.String())),
		zap.String("Method", r.Method),
		zap.Int("ProccessId", os.Getpid()),
	)

	// check if the content type is correct
	if r.Header.Get("Content-Type") != ContentType {
		err := fmt.Errorf("incorrect Content-Type %s. Content must be %s",
			r.Header.Get("Content-Type"),
			ContentType,
		)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Incorrect Content-Type",
			zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", ContentType),
			zap.Error(err),
		)

		return http.StatusBadRequest, err
	}

	if r.Method != http.MethodPut {
		err := fmt.Errorf("invalid HTTP Method: %s, must be HTTP Metho: %s",
			r.Method,
			http.MethodPut,
		)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Invalid HTTP Method",
			zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut),
			zap.Error(err),
		)

		return http.StatusMethodNotAllowed, err
	}

	return http.StatusOK, nil
}

func readAndDeserialize(body io.Reader) (messages.IProxyMessage, error) {

	// create an empty []byte and read the
	// request body into it if not nil
	payload, err := ioutil.ReadAll(body)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Null request body", zap.String("Error", err.Error()))
		return nil, err
	}

	// deserialize the payload
	buf := bytes.NewBuffer(payload)
	message, err := messages.Deserialize(buf, false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error deserializing input", zap.Error(err))
		return nil, err
	}

	return message, nil
}
