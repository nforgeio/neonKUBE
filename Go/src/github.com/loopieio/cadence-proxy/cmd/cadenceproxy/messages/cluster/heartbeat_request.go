package cluster

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// HeartbeatRequest is ProxyRequest of MessageType
	// HeartbeatRequest.
	//
	// A HeartbeatRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	HeartbeatRequest struct {
		*base.ProxyRequest
		ReplyType messages.MessageType
	}
)

// NewHeartbeatRequest is the default constructor for
// HeartbeatRequest
//
// returns *HeartbeatRequest -> pointer to a newly initialized
// HeartbeatReqeuest in memory
func NewHeartbeatRequest() *HeartbeatRequest {
	request := new(HeartbeatRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.HeartbeatRequest
	request.ReplyType = messages.HeartbeatReply
	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *HeartbeatRequest) Clone() base.IProxyMessage {
	heartbeatRequest := NewHeartbeatRequest()
	var messageClone base.IProxyMessage = heartbeatRequest
	request.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *HeartbeatRequest) CopyTo(target base.IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *HeartbeatRequest) SetProxyMessage(value *base.ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *HeartbeatRequest) GetProxyMessage() *base.ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *HeartbeatRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyRequest.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *HeartbeatRequest) GetRequestID() int64 {
	return request.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *HeartbeatRequest) SetRequestID(value int64) {
	request.SetLongProperty("RequestId", value)
}

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *HeartbeatRequest) GetReplyType() messages.MessageType {
	return request.ReplyType
}
