package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// CancelRequest is ProxyRequest of MessageType
	// CancelRequest.
	//
	// A CancelRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	CancelRequest struct {
		*ProxyRequest
	}
)

// NewCancelRequest is the default constructor for a CancelRequest
//
// returns *CancelRequest -> a reference to a newly initialized
// CancelRequest in memory
func NewCancelRequest() *CancelRequest {
	request := new(CancelRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.CancelRequest
	request.SetReplyType(messagetypes.CancelReply)

	return request
}

// GetTargetRequestID gets a CancelRequest's TargetRequestId value
// from its properties map
//
// returns int64 -> a long representing the target to cancels requestID that is
// in a CancelRequest's properties map
func (request *CancelRequest) GetTargetRequestID() int64 {
	return request.GetLongProperty("TargetRequestId")
}

// SetTargetRequestID sets a CancelRequest's TargetRequestId value
// in its properties map
//
// param value int64 -> a long value to be set in the properties map as a
// CancelRequest's TargetRequestId
func (request *CancelRequest) SetTargetRequestID(value int64) {
	request.SetLongProperty("TargetRequestId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *CancelRequest) Clone() IProxyMessage {
	cancelRequest := NewCancelRequest()
	var messageClone IProxyMessage = cancelRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *CancelRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*CancelRequest); ok {
		v.SetTargetRequestID(request.GetTargetRequestID())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *CancelRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *CancelRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *CancelRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *CancelRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *CancelRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *CancelRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *CancelRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *CancelRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
