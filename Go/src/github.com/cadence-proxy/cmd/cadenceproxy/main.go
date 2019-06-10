//-----------------------------------------------------------------------------
// FILE:		main.go
// CONTRIBUTOR: John C Burnes
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

package main

import (
	"flag"

	"github.com/cadence-proxy/internal/endpoints"
	"github.com/cadence-proxy/internal/logger"
	"github.com/cadence-proxy/internal/server"
)

var (

	// variables to put command line args in
	address, logLevel string
	debugMode         bool

	// INTERNAL USE ONLY: Optionally indicates that the cadence-proxy will
	// already be running for debugging purposes.  When this is true, the
	// cadence-client be hardcoded to listen on 127.0.0.2:5001 and
	// the cadence-proxy will be assumed to be listening on 127.0.0.2:5000.
	// This defaults to false.
	debugPrelaunch = true
)

func main() {

	// define the flags and parse them
	flag.StringVar(&address, "listen", "127.0.0.2:5000", "Address for the Cadence Proxy Server to listen on")
	flag.StringVar(&logLevel, "log-level", "info", "The log level when running the proxy")
	flag.BoolVar(&debugMode, "debug", true, "Set to debug mode")
	flag.Parse()

	// endpoint debugging
	if debugMode {
		endpoints.Debug = true
	}

	// debug prelaunched
	if debugPrelaunch {
		endpoints.DebugPrelaunch = true
	}

	// set the log level and if the program should run in debug mode
	logger.SetLogger(logLevel, debugMode)

	// create the instance, set the routes,
	// and start the server
	instance := server.NewInstance(address)

	// set server instance variable in endpoints package
	endpoints.Instance = instance

	// setup the routes
	endpoints.SetupRoutes(instance.Router)

	// start the server
	instance.Start()
}
