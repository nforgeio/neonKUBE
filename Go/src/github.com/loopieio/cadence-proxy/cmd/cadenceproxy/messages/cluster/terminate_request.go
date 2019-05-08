package cluster

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// TerminateRequest is ProxyRequest of MessageType
	// TerminateRequest.
	//
	// A TerminateRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	TerminateRequest struct {
		*base.ProxyRequest
		ReplyType messages.MessageType
	}
)

// NewTerminateRequest is the default constructor for
// TerminateRequest
//
// returns *TerminateRequest -> pointer to a newly initialized
// TerminateReqeuest in memory
func NewTerminateRequest() *TerminateRequest {
	request := new(TerminateRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.TerminateRequest
	request.ReplyType = messages.TerminateReply
	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *TerminateRequest) Clone() base.IProxyMessage {
	terminateRequest := NewTerminateRequest()
	var messageClone base.IProxyMessage = terminateRequest
	request.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *TerminateRequest) CopyTo(target base.IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *TerminateRequest) SetProxyMessage(value *base.ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *TerminateRequest) GetProxyMessage() *base.ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *TerminateRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyRequest.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *TerminateRequest) GetRequestID() int64 {
	return request.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *TerminateRequest) SetRequestID(value int64) {
	request.SetLongProperty("RequestId", value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *TerminateRequest) GetReplyType() messages.MessageType {
	return request.ReplyType
}
