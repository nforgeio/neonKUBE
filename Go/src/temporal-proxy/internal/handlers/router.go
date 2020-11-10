//-----------------------------------------------------------------------------
// FILE:		router.go
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

	"github.com/go-chi/chi"
	"github.com/go-chi/chi/middleware"
)

// Debug is a bool value that is set in main
// it indicates whether or not to use debugging middleware
// in the chi.Router
var Debug bool

// SetupRoutes sets up the chi middleware
// and the route tree
func SetupRoutes(router *chi.Mux) {

	// Group the 2 endpoint routes together to utilize
	// same middleware stack
	router.Group(func(router chi.Router) {

		// Set middleware for the chi.Router to use:
		// RequestID
		// Recoverer
		router.Use(middleware.RequestID)
		router.Use(middleware.Recoverer)

		// cadence-proxy endpoints
		router.Put("/", MessageHandler)
		router.Put("/echo", EchoHandler)

		// endpoints for test paths
		router.Mount("/test", TestRouter())
	})
}

//TestRouter that one could ping to test if the API is alive
func TestRouter() http.Handler {
	router := chi.NewRouter()

	router.Get("/", func(w http.ResponseWriter, r *http.Request) {
		_, err := w.Write([]byte("WE ARE HERE, WE ARE HERE, WE ARE HERE!!!!"))
		if err != nil {
			panic(err)
		}
	})

	router.Get("/ping", func(w http.ResponseWriter, r *http.Request) {
		_, err := w.Write([]byte("pong"))
		if err != nil {
			panic(err)
		}
	})

	router.Get("/panic", func(w http.ResponseWriter, r *http.Request) {
		panic("test")
	})

	return router
}
