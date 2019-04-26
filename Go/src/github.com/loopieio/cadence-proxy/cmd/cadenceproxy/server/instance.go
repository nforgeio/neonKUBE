package server

import (
	"context"
	"fmt"
	"net/http"
	"time"

	"github.com/go-chi/chi"
	"github.com/go-chi/chi/middleware"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/endpoints"
	"go.uber.org/zap"
)

const (
	_version    = "/v1"
	_rootPath   = "/cadence-proxy"
	_pathPrefix = fmt.Sprintf("/api%s%s", _version, _rootPath)
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
// Configures the Router's routes,
// ListenAndServers on the configured server address,
// and provides functionality for a clean shutdown if the server
// shuts down unexpectedly
func (s *Instance) Start() {

	// Startup all dependencies
	// Panic if anything essential fails
	logger, err := zap.NewDevelopment()
	if err != nil {
		panic(err)
	}
	logger.Info("Logger created.")
	s.Logger = logger

	// set the routes for the Instance.Router
	s.setupRoutes()

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

// SetupRoutes sets up the chi middleware
// and the route tree
func (s *Instance) setupRoutes() {

	// Group the 2 endpoint routes together to utilize
	// same middleware stack
	s.Router.Group(func(router chi.Router) {

		// Set middleware for the chi.Router to use:
		// RequestID
		// Logger
		// Recoverer
		router.Use(middleware.RequestID)
		router.Use(middleware.Logger)
		router.Use(middleware.Recoverer)

		// Set the route for the chi.Router to pathPrefix
		router.Route(_pathPrefix, func(router chi.Router) {

			// cadence-proxy endpoints
			router.Put("/", endpoints.ProxyMessageHandler)
			router.Put("/echo", endpoints.EchoHandler)

			// endpoints for test paths
			router.Mount("/test", endpoints.TestRouter())
		})
	})
}
