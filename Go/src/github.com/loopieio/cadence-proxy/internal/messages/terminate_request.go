package messages

import (
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
	request.SetType(messagetypes.TerminateRequest)
	request.SetReplyType(messagetypes.TerminateReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *TerminateRequest) Clone() IProxyMessage {
	terminateRequest := NewTerminateRequest()
	var messageClone IProxyMessage = terminateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *TerminateRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}
