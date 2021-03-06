//-----------------------------------------------------------------------------
// FILE:		instance.go
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

package server

import (
	"context"
	"net/http"
	"os"
	"time"

	"go.uber.org/zap"

	"github.com/go-chi/chi"
)

type (
	// InstanceConfig takes config values to
	// create a new Instance
	InstanceConfig struct {
		Address        string
		Logger         *zap.Logger
		MessageHandler func(w http.ResponseWriter, r *http.Request)
		EchoHandler    func(w http.ResponseWriter, r *http.Request)
	}

	// Instance is a server instance that contains
	// a reference to an http.Server in memory,
	// a reference to an existing zap.Logger,
	// and a reference to an existing chi.Mux
	Instance struct {
		httpServer      *http.Server
		Logger          *zap.Logger
		Router          *chi.Mux
		ShutdownChannel chan bool
	}
)

// NewInstance initializes a new instance of the server Instance
// from a config.
func NewInstance(config *InstanceConfig) *Instance {
	router := chi.NewRouter()
	logger := config.Logger

	var err error
	if logger == nil {
		logger, err = zap.NewDevelopment()
	}

	if err != nil {
		panic(err)
	}

	s := &Instance{
		Router:          router,
		httpServer:      &http.Server{Addr: config.Address, Handler: router},
		ShutdownChannel: make(chan bool),
		Logger:          logger.Named("server   "),
	}

	SetupRoutes(s.Router, config.MessageHandler, config.EchoHandler)

	return s
}

// Start sets a zap.Logger for a server Instance
// ListenAndServers on the configured server address,
// and provides functionality for a clean shutdown if the server
// shuts down unexpectedly
func (s *Instance) Start() {
	s.Logger.Info("Server Details",
		zap.String("Address", s.httpServer.Addr),
		zap.Int("ProcessId", os.Getpid()),
	)

	// defer behaviors
	defer func() {

		// sync logger before exit
		// and cancel context
		close(s.ShutdownChannel)
		err := s.Logger.Sync()
		if err != nil {
			s.Logger.Error("Error", zap.Error(err))
		}
	}()

	// listen and serve (for your country)
	go func() {
		if err := s.httpServer.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			s.Logger.Fatal("http server stopped unexpenctedly", zap.Error(err))
		}
	}()

	// wait for the shutdown signal from a terminate request
	shutdown := <-s.ShutdownChannel
	if shutdown {

		// create the context and the cancelFunc to shut down the server instance
		ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
		defer cancel()

		// try and do a graceful shutdown if possible from context
		if err := s.httpServer.Shutdown(ctx); err != nil {
			s.Logger.Fatal("could not gracefully shut server down", zap.Error(err))
		}
		s.Logger.Info("server gracefully shutting down")
	}
}
