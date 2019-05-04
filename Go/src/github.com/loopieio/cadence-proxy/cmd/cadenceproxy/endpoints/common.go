package endpoints

import (
	"fmt"
	"net/http"
	"os"
	"sync"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/server"
	"go.uber.org/zap"
)

const (

	// _contentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	_contentType = "application/x-neon-cadence-proxy"
)

var (
	requestmu sync.RWMutex

	// NextRequestID is incremented (protected by a mutex) every time
	// a new request message is sent
	NextRequestID int64

	// logger for all endpoints to utilize
	logger *zap.Logger

	// Instance is a pointer to the server instance of the current server that the
	// cadence-proxy is listening on.  This gets set in main.go
	Instance *server.Instance
)

// IncrementNextRequestID increments the global variable
// NextRequestID by 1 and is protected by a mutex lock
func IncrementNextRequestID() {
	requestmu.Lock()
	NextRequestID = NextRequestID + 1
	requestmu.Unlock()
}

// GetNextRequestID gets the value of the global variable
// NextRequestID and is protected by a mutex Read lock
func GetNextRequestID() int64 {
	requestmu.RLock()
	defer requestmu.RUnlock()
	return NextRequestID
}

func checkRequestValidity(w http.ResponseWriter, r *http.Request) (int, error) {

	// log when a new request has come in
	logger.Info("Request Recieved",
		zap.String("Address", fmt.Sprintf("http://%s%s", r.Host, r.URL.String())),
		zap.String("Method", r.Method),
		zap.Int("ProccessId", os.Getpid()),
	)

	// check if the content type is correct
	if r.Header.Get("Content-Type") != _contentType {
		err := fmt.Errorf("incorrect Content-Type %s. Content must be %s",
			r.Header.Get("Content-Type"),
			_contentType,
		)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Incorrect Content-Type",
			zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", _contentType),
			zap.Error(err),
		)

		return http.StatusBadRequest, err
	}

	if r.Method != http.MethodPut {
		err := fmt.Errorf("invalid HTTP Method: %s, must be HTTP Metho: %s",
			r.Method,
			http.MethodPut,
		)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Invalid HTTP Method",
			zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut),
			zap.Error(err),
		)

		return http.StatusMethodNotAllowed, err
	}

	return http.StatusOK, nil
}
