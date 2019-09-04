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

package internal

import (
	"errors"
)

const (

	// ContentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	ContentType = "application/x-neon-cadence-proxy"

	// CadenceLoggerName is the name of the zap.Logger that will
	// log internal cadence messages.
	CadenceLoggerName = "cadence-internal"

	// ProxyLoggerName is the name of the zap.Logger that will
	// log internal cadence-proxy messages.
	ProxyLoggerName = "cadence-proxy"
)

var (

	// DebugPrelaunched INTERNAL USE ONLY: Optionally indicates that the cadence-proxy will
	// already be running for debugging purposes.  When this is true, the
	// cadence-client be hardcoded to listen on 127.0.0.2:5001 and
	// the cadence-proxy will be assumed to be listening on 127.0.0.2:5000.
	// This defaults to false.
	DebugPrelaunched = false

	// Debug indicates that the proxy is running in Debug mode.  This
	// is used to configure specified settings.
	Debug = false

	// ErrConnection is the custom error that is thrown when the cadence-proxy
	// is not able to establish a connection with the cadence server
	ErrConnection = errors.New("CadenceConnectionError{Messages: Could not establish a connection with the cadence server.}")

	// ErrEntityNotExist is the custom error that is thrown when a cadence
	// entity cannot be found in the cadence server
	ErrEntityNotExist = errors.New("EntityNotExistsError{Message: The entity you are looking for does not exist.}")

	// ErrArgumentNil is the custom error that is thrown when trying to access a nil
	// value
	ErrArgumentNil = errors.New("ArgumentNilError{Message: failed to access nil value.}")
)
