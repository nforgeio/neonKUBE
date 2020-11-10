//-----------------------------------------------------------------------------
// FILE:		vars.go
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

package handlers

import (
	"net/http"

	"go.uber.org/zap"

	"temporal-proxy/internal/server"
	proxyactivity "temporal-proxy/internal/temporal/activity"
	proxyclient "temporal-proxy/internal/temporal/client"
	proxyworkflow "temporal-proxy/internal/temporal/workflow"
)

var (
	// Logger for use in endpoints use in project
	Logger *zap.Logger

	// Instance is a pointer to the server instance of the current server that the
	// temporal-proxy is listening on.  This gets set in main.go
	Instance *server.Instance

	// HttpClient is the HTTP client used to send requests
	// to the Neon.Temporal client
	HttpClient http.Client

	// LoggerClientID is the int64 id used to send LogRequests
	// to the Neon.Temporal client
	LoggerClientID int64

	// ReplyAddress specifies the address that the Neon.Temporal library
	// will be listening on for replies from the temporal proxy
	replyAddress string

	// terminate is a boolean that will be set after handling an incoming
	// TerminateRequest.  A true value will indicate that the server instance
	// needs to gracefully shut down after handling the request, and a false value
	// indicates the server continues to run
	terminate bool

	// ActivityContexts maps a int64 ContextId to the temporal
	// Activity Context passed to the temporal Activity functions.
	// The temporal-client will use contextIds to refer to specific
	// activity contexts when perfoming activity actions
	ActivityContexts = proxyactivity.NewActivityContextsMap()

	// WorkflowContexts maps a int64 ContextId to the temporal
	// Workflow Context passed to the temporal Workflow functions.
	// The temporal-client will use contextIds to refer to specific
	// workflow ocntexts when perfoming workflow actions
	WorkflowContexts = proxyworkflow.NewWorkflowContextsMap()

	// Operations is a map of operations used to track pending
	// temporal-client operations
	Operations = NewOperationsMap()

	// Clients is a map of ClientHelpers to ClientID used to
	// store ClientHelpers to support multiple clients
	Clients = proxyclient.NewClientsMap()
)
