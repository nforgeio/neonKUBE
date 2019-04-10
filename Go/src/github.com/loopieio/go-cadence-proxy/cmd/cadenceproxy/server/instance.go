package server

import (
	"context"
	"net/http"
	"time"

	"github.com/go-chi/chi"
	"go.uber.org/zap"
)

// Instance is a server instance
// Can also hold DB instance and logger
type Instance struct {
	httpServer *http.Server
	Logger     *zap.Logger
	Router     *chi.Mux
}

// NewInstance initializes a new instance of the server Instance
// Param addr string -> the desired address for the server to
// listen and serve on
// Returns *Instance -> Pointer to an Instance object
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

// Start starts the server instance
func (s *Instance) Start() {

	// Startup all dependencies
	// Panic if anything essential fails
	logger, err := zap.NewDevelopment()
	if err != nil {
		panic(err)
	}
	logger.Info("Logger created.")
	s.Logger = logger

	// setup the routes
	s.SetupRoutes()

	// listen and serve (for your country)
	err = s.httpServer.ListenAndServe()

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

// GetAddress is a getter for an instances
// server address
func (s *Instance) GetAddress() string {
	return s.httpServer.Addr
}
