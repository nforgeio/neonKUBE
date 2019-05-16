package endpoints

import (
	"bytes"
	"errors"
	"fmt"
	"io"
	"io/ioutil"
	"net/http"
	"os"
	"sync"
	"time"

	"github.com/loopieio/cadence-proxy/internal/messages"
	"github.com/loopieio/cadence-proxy/internal/server"
	"go.uber.org/zap"
)

const (

	// ContentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	ContentType = "application/x-neon-cadence-proxy"

	// _cadenceSystemDomain is the string name of the cadence-system domain that
	// exists on all cadence servers.  This value is used to check that a connection
	// has been established to the cadence server instance and that it is ready to
	// accept requests
	_cadenceSystemDomain = "cadence-system"

	// _cadenceTimeout specifies the amount of time in seconds a reply has to be sent after
	// a request has been recieved by the cadence-proxy
	_cadenceTimeout = time.Second * 30
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

	// connectionError is the custom error that is thrown when the cadence-proxy
	// is not able to establish a connection with the cadence server
	connectionError = errors.New("CadenceConnectionError{Messages: Could not establish a connection with the cadence server.}")

	// replyAddress specifies the address that the Neon.Cadence library
	// will be listening on for replies from the cadence proxy
	replyAddress string

	// terminate is a boolean that will be set after handling an incoming
	// TerminateRequest.  A true value will indicate that the server instance
	// needs to gracefully shut down after handling the request, and a false value
	// indicates the server continues to run
	terminate bool

	// INTERNAL USE ONLY:</b> Optionally indicates that the <b>cadence-client</b>
	// will not perform the <see cref="InitializeRequest"/>/<see cref="InitializeReply"/>
	// and <see cref="TerminateRequest"/>/<see cref="TerminateReply"/> handshakes
	// with the <b>cadence-proxy</b> for debugging purposes.  This defaults to
	// <c>false</c>
	debugPrelaunch = false
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
	if r.Header.Get("Content-Type") != ContentType {
		err := fmt.Errorf("incorrect Content-Type %s. Content must be %s",
			r.Header.Get("Content-Type"),
			ContentType,
		)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Incorrect Content-Type",
			zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", ContentType),
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

func readAndDeserialize(body io.Reader) (messages.IProxyMessage, error) {

	// create an empty []byte and read the
	// request body into it if not nil
	payload, err := ioutil.ReadAll(body)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Null request body", zap.String("Error", err.Error()))
		return nil, err
	}

	// deserialize the payload
	buf := bytes.NewBuffer(payload)
	message, err := messages.Deserialize(buf, false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error deserializing input", zap.Error(err))
		return nil, err
	}

	return message, nil
}
