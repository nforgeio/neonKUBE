package messages

import (
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
	request.SetType(messagetypes.PingRequest)
	request.SetReplyType(messagetypes.PingReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (request *PingRequest) Clone() IProxyMessage {
	pingRequest := NewPingRequest()
	var messageClone IProxyMessage = pingRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (request *PingRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}
