package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// PingRequest is ProxyRequest of MessageType
	// PingRequest.
	//
	// A PingRequest contains a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	PingRequest struct {
		*ProxyRequest
	}
)

// NewPingRequest is the default constructor for
// PingRequest
//
// returns *PingRequest -> pointer to a newly initialized
// PingReqeuest in memory
func NewPingRequest() *PingRequest {
	request := new(PingRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.PingRequest
	request.SetReplyType(messagetypes.PingReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *PingRequest) Clone() IProxyMessage {
	pingRequest := NewPingRequest()
	var messageClone IProxyMessage = pingRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *PingRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *PingRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *PingRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *PingRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *PingRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *PingRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *PingRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *PingRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *PingRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
