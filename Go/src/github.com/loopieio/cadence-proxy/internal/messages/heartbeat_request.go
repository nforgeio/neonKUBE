package messages

import (
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
	request.SetType(messagetypes.HeartbeatRequest)
	request.SetReplyType(messagetypes.HeartbeatReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *HeartbeatRequest) Clone() IProxyMessage {
	heartbeatRequest := NewHeartbeatRequest()
	var messageClone IProxyMessage = heartbeatRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *HeartbeatRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}
