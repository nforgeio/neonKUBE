package endpoints

import (
	"net/http"

	"github.com/go-chi/chi"
)

//TestRouter that one could ping to test if the API is alive
func TestRouter() http.Handler {
	router := chi.NewRouter()

	router.Get("/", func(w http.ResponseWriter, r *http.Request) {
		w.Write([]byte("WE ARE HERE, WE ARE HERE, WE ARE HERE!!!!"))
	})

	router.Get("/ping", func(w http.ResponseWriter, r *http.Request) {
		w.Write([]byte("pong"))
	})

	router.Get("/panic", func(w http.ResponseWriter, r *http.Request) {
		panic("test")
	})

	return router
}
