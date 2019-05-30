package messages

import (
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
