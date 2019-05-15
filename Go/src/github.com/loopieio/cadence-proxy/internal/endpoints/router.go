package endpoints

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

		if Debug {
			router.Use(middleware.Logger)
		}

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
