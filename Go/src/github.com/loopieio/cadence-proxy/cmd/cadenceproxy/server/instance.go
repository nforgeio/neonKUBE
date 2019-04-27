package server

import (
	"context"
	"net/http"
	"os"
	"time"

	"github.com/go-chi/chi"
	"go.uber.org/zap"
)

// Instance is a server instance that contains
// a reference to an http.Server in memory,
// a reference to an existing zap.Logger,
// and a reference to an existing chi.Mux
type Instance struct {
	httpServer *http.Server
	Logger     *zap.Logger
	Router     *chi.Mux
}

// NewInstance initializes a new instance of the server Instance
//
// param addr string -> the desired address for the server to
// listen and serve on
//
// returns *Instance -> Pointer to an Instance object
func NewInstance(addr string) *Instance {

	// Router defines new chi router to set up the routes
	var router = chi.NewRouter()

	// do any server instance setup here
	s := &Instance{
		Router:     router,
		httpServer: &http.Server{Addr: addr, Handler: router},
	}

	return s
}

// Start sets a zap.Logger for a server Instance
// ListenAndServers on the configured server address,
// and provides functionality for a clean shutdown if the server
// shuts down unexpectedly
func (s *Instance) Start() {

	// set the logger to the global
	// zap.Logger
	s.Logger = zap.L()
	s.Logger.Info("Server listening:",
		zap.String("Address", s.httpServer.Addr),
		zap.Int("ProccessId", os.Getpid()))

	// listen and serve (for your country)
	err := s.httpServer.ListenAndServe()

	// Clean shutdown is the server unexpectedly shuts down
	if err != http.ErrServerClosed {
		s.Logger.Error("Http Server Stopped Unexpenctedly", zap.Error(err))
		s.Shutdown()
	} else {
		s.Logger.Error("Http Server Stoppped", zap.Error(err))
	}
}

// Shutdown shuts the server instance down gracefully if possible
func (s *Instance) Shutdown() {
	if s.httpServer != nil {
		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()

		err := s.httpServer.Shutdown(ctx)
		if err != nil {
			s.Logger.Error("Failed to shutdown http server gracefully", zap.Error(err))
		} else {
			s.httpServer = nil
		}
	}
}
