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

	"go.uber.org/cadence"
	"go.uber.org/cadence/workflow"

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
)

var (
	mu sync.RWMutex

	// requestID is incremented (protected by a mutex) every time
	// a new request message is sent
	requestID int64

	// logger for all endpoints to utilize
	logger *zap.Logger

	// Instance is a pointer to the server instance of the current server that the
	// cadence-proxy is listening on.  This gets set in main.go
	Instance *server.Instance

	// connectionError is the custom error that is thrown when the cadence-proxy
	// is not able to establish a connection with the cadence server
	connectionError = errors.New("CadenceConnectionError{Messages: Could not establish a connection with the cadence server.}")

	// entityNotExistError is the custom error that is thrown when a cadence
	// entity cannot be found in the cadence server
	entityNotExistError = errors.New("EntityNotExistsError{Message: The entity you are looking for does not exist.}")

	// argumentNullError is the custom error that is thrown when trying to access a nil
	// value
	argumentNilError = errors.New("ArgumentNilError{Message: failed to access nil value.}")

	// failWithError is the custom error that is thrown when we need to signal an
	// Operation to fail with an error
	failWithError = errors.New("FailWithError{Message: induce failure.")

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
	debugPrelaunch = true

	// cadenceClientTimeout specifies the amount of time in seconds a reply has to be sent after
	// a request has been recieved by the cadence-proxy
	cadenceClientTimeout time.Duration

	// Operations is a map of operations used to track pending
	// cadence-client operations
	Operations = new(operationsMap)
)

type (
	operationsMap struct {
		sync.Map
	}

	// Operation is used to track pending Neon.Cadence library calls
	Operation struct {
		future      workflow.Future
		settable    workflow.Settable
		requestID   int64
		request     messages.IProxyRequest
		isCancelled bool
	}
)

// NewOperation is the default constructor for an Operation
func NewOperation(requestID int64, request messages.IProxyRequest) *Operation {
	op := new(Operation)
	op.isCancelled = false
	op.request = request
	op.requestID = requestID

	return op
}

//----------------------------------------------------------------------------
// Operation instance methods

// GetIsCancelled gets isCancelled
func (op *Operation) GetIsCancelled() bool {
	return op.isCancelled
}

// SetIsCancelled sets isCancelled
func (op *Operation) SetIsCancelled(value bool) {
	op.isCancelled = value
}

// GetRequestID gets the requestID
func (op *Operation) GetRequestID() int64 {
	return op.requestID
}

// SetRequestID sets the requestID
func (op *Operation) SetRequestID(value int64) {
	op.requestID = value
}

// GetRequest gets the request
func (op *Operation) GetRequest() messages.IProxyRequest {
	return op.request
}

// SetRequest sets the request
func (op *Operation) SetRequest(value messages.IProxyRequest) {
	op.request = value
}

// GetFuture gets a Operation's workflow.Future
//
// returns workflow.Future -> a cadence workflow.Future
func (op *Operation) GetFuture() workflow.Future {
	return op.future
}

// SetFuture sets a Operation's workflow.Future
//
// param value workflow.Future -> a cadence workflow.Future to be
// set as a Operation's cadence workflow.Future
func (op *Operation) SetFuture(value workflow.Future) {
	op.future = value
}

// GetSettable gets a Operation's workflow.Settable
//
// returns workflow.Settable -> a cadence workflow.Settable
func (op *Operation) GetSettable() workflow.Settable {
	return op.settable
}

// SetSettable sets a Operation's workflow.Settable
//
// param value workflow.Settable -> a cadence workflow.Settable to be
// set as a Operation's cadence workflow.Settable
func (op *Operation) SetSettable(value workflow.Settable) {
	op.settable = value
}

// SetReply signals the awaiting task that a workflow reply message
// has been received
func (op *Operation) SetReply(value messages.IProxyReply, result interface{}) error {
	if op.future == nil {
		return argumentNilError
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Checking if Future is ready", zap.Bool("Future IsReady", op.future.IsReady()))

	settable := op.GetSettable()
	if err := value.GetError(); err != nil {
		settable.Set(nil, cadence.NewCustomError(err.ToString()))
	} else {
		settable.Set(result, nil)
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Checking if Future is ready", zap.Bool("Future IsReady", op.future.IsReady()))

	return nil
}

// SetCancelled signals the awaiting task that the Operation has
// been canceled
func (op *Operation) SetCancelled() {
	op.isCancelled = true
}

// SetError signals the awaiting task that it should fails with an
// error
func (op *Operation) SetError(value error) error {
	if op.future == nil {
		return argumentNilError
	}

	settable := op.GetSettable()
	settable.Set(nil, cadence.NewCustomError(failWithError.Error()))

	return nil
}

//----------------------------------------------------------------------------
// RequestID thread-safe methods

// NextRequestID increments the package variable
// requestID by 1 and is protected by a mutex lock
func NextRequestID() int64 {
	mu.Lock()
	curr := requestID
	requestID = requestID + 1
	mu.Unlock()

	return curr
}

// GetRequestID gets the value of the global variable
// requestID and is protected by a mutex Read lock
func GetRequestID() int64 {
	mu.RLock()
	defer mu.RUnlock()
	return requestID
}

//----------------------------------------------------------------------------
// operationsMap instance methods

// Add adds a new Operation and its corresponding requestID into
// the operationsMap.  This method is thread-safe.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// param value *Operation -> pointer to Operation to be set in the map.
// This will be the mapped value
//
// returns int64 -> requestID of the request being added
// in the Operation at the specified requestID
func (opMap *operationsMap) Add(requestID int64, value *Operation) int64 {
	opMap.Store(requestID, value)
	return requestID
}

// Remove removes key/value entry from the operationsMap at the specified
// requestID.  This is a thread-safe method.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// returns int64 -> requestID of the request being removed in the
// Operation at the specified requestID
func (opMap *operationsMap) Remove(requestID int64) int64 {
	opMap.Delete(requestID)
	return requestID
}

// Get gets a Operation from the operationsMap at the specified
// requestID.  This method is thread-safe.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// returns *Operation -> pointer to Operation at the specified requestID
// in the map.
func (opMap *operationsMap) Get(requestID int64) *Operation {
	if v, ok := opMap.Load(requestID); ok {
		if _v, _ok := v.(*Operation); _ok {
			return _v
		}
	}

	return nil
}

//----------------------------------------------------------------------------
// ProxyMessage processing helpers

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
