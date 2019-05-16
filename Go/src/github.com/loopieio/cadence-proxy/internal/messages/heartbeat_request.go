package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// HeartbeatRequest is ProxyRequest of MessageType
	// HeartbeatRequest.
	//
	// A HeartbeatRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	HeartbeatRequest struct {
		*ProxyRequest
	}
)

// NewHeartbeatRequest is the default constructor for
// HeartbeatRequest
//
// returns *HeartbeatRequest -> pointer to a newly initialized
// HeartbeatReqeuest in memory
func NewHeartbeatRequest() *HeartbeatRequest {
	request := new(HeartbeatRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.HeartbeatRequest
	request.SetReplyType(messagetypes.HeartbeatReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *HeartbeatRequest) Clone() IProxyMessage {
	heartbeatRequest := NewHeartbeatRequest()
	var messageClone IProxyMessage = heartbeatRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *HeartbeatRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *HeartbeatRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *HeartbeatRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *HeartbeatRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *HeartbeatRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *HeartbeatRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *HeartbeatRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *HeartbeatRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *HeartbeatRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
