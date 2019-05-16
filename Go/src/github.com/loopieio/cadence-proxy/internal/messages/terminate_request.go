package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// TerminateRequest is ProxyRequest of MessageType
	// TerminateRequest.
	//
	// A TerminateRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	TerminateRequest struct {
		*ProxyRequest
	}
)

// NewTerminateRequest is the default constructor for
// TerminateRequest
//
// returns *TerminateRequest -> pointer to a newly initialized
// TerminateReqeuest in memory
func NewTerminateRequest() *TerminateRequest {
	request := new(TerminateRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.TerminateRequest
	request.SetReplyType(messagetypes.TerminateReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *TerminateRequest) Clone() IProxyMessage {
	terminateRequest := NewTerminateRequest()
	var messageClone IProxyMessage = terminateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *TerminateRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *TerminateRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *TerminateRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *TerminateRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *TerminateRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *TerminateRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *TerminateRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *TerminateRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *TerminateRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
