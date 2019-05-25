package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// StopWorkerRequest is ProxyRequest of MessageType
	// StopWorkerRequest.
	//
	// A StopWorkerRequest contains a reference to a
	// ProxyReply struct in memory
	StopWorkerRequest struct {
		*ProxyRequest
	}
)

// NewStopWorkerRequest is the default constructor for a StopWorkerRequest
//
// returns *StopWorkerRequest -> a reference to a newly initialized
// StopWorkerRequest in memory
func NewStopWorkerRequest() *StopWorkerRequest {
	request := new(StopWorkerRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.StopWorkerRequest)
	request.SetReplyType(messagetypes.StopWorkerReply)

	return request
}

// GetWorkerID gets a StopWorkerRequest's WorkerId value
// from its properties map.  A WorkerId identifies the
// worker to be stopped
//
// returns int64 -> a long representing the target to cancels requestID that is
// in a StopWorkerRequest's properties map
func (request *StopWorkerRequest) GetWorkerID() int64 {
	return request.GetLongProperty("WorkerId")
}

// SetWorkerID sets a StopWorkerRequest's WorkerId value
// in its properties map. A WorkerId identifies the
// worker to be stopped
//
// param value int64 -> a long value to be set in the properties map as a
// StopWorkerRequest's WorkerId
func (request *StopWorkerRequest) SetWorkerID(value int64) {
	request.SetLongProperty("WorkerId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *StopWorkerRequest) Clone() IProxyMessage {
	stopWorkerRequest := NewStopWorkerRequest()
	var messageClone IProxyMessage = stopWorkerRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *StopWorkerRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*StopWorkerRequest); ok {
		v.SetWorkerID(request.GetWorkerID())
	}
}

// SetProxyMessage inherits docs from ProxyRequest.SetProxyMessage()
func (request *StopWorkerRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyRequest.GetProxyMessage()
func (request *StopWorkerRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *StopWorkerRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *StopWorkerRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// GetType inherits docs from ProxyRequest.GetType()
func (request *StopWorkerRequest) GetType() messagetypes.MessageType {
	return request.ProxyRequest.GetType()
}

// SetType inherits docs from ProxyRequest.SetType()
func (request *StopWorkerRequest) SetType(value messagetypes.MessageType) {
	request.ProxyRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *StopWorkerRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *StopWorkerRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *StopWorkerRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *StopWorkerRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
