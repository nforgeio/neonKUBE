package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
	worker "go.uber.org/cadence/worker"
)

type (

	// NewWorkerRequest is ProxyRequest of MessageType
	// NewWorkerRequest.
	//
	// A NewWorkerRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	NewWorkerRequest struct {
		*ProxyRequest
	}
)

// NewNewWorkerRequest is the default constructor for a NewWorkerRequest
//
// returns *NewWorkerRequest -> a reference to a newly initialized
// NewWorkerRequest in memory
func NewNewWorkerRequest() *NewWorkerRequest {
	request := new(NewWorkerRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.NewWorkerRequest
	request.SetReplyType(messagetypes.NewWorkerReply)

	return request
}

// GetDomain gets a NewWorkerRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's Domain
func (request *NewWorkerRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a NewWorkerRequest's Domain value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetTaskList gets a NewWorkerRequest's TaskList value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's TaskList
func (request *NewWorkerRequest) GetTaskList() *string {
	return request.GetStringProperty("TaskList")
}

// SetTaskList sets a NewWorkerRequest's TaskList value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetTaskList(value *string) {
	request.SetStringProperty("TaskList", value)
}

// GetOptions gets a NewWorkerRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// returns *worker.WorkerOptions -> pointer to a cadence worker struct that contains the
// options for creating a new worker
func (request *NewWorkerRequest) GetOptions() *worker.Options {
	opts := new(worker.Options)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a NewWorkerRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// param value client.StartWorkflowOptions -> pointer to a cadence worker struct
// that contains the options for creating a new worker to be set in the NewWorkerRequest's
// properties map
func (request *NewWorkerRequest) SetOptions(value *worker.Options) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *NewWorkerRequest) Clone() IProxyMessage {
	newWorkerRequest := NewNewWorkerRequest()
	var messageClone IProxyMessage = newWorkerRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *NewWorkerRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*NewWorkerRequest); ok {
		v.SetDomain(request.GetDomain())
		v.SetTaskList(request.GetTaskList())
		v.SetOptions(request.GetOptions())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *NewWorkerRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *NewWorkerRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *NewWorkerRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *NewWorkerRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *NewWorkerRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *NewWorkerRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *NewWorkerRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *NewWorkerRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
