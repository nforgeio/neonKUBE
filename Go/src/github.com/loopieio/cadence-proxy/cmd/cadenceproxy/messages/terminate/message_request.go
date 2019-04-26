package terminate

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// TerminateRequest is a ProxyRequest of MessageType
	// TerminateRequest It holds a reference to a
	// ProxyRequest in memory
	TerminateRequest struct {
		*base.ProxyRequest
	}
)

// InitTerminate is a method that adds a key/value entry into the
// IntToMessageStruct at keys TerminateRequest and TerminateReply.
// The values are new instances of a TerminateRequest and TerminateReply
func InitTerminate() {
	key := int(messages.TerminateRequest)
	base.IntToMessageStruct[key] = NewTerminateRequest()

	key = int(messages.TerminateReply)
	base.IntToMessageStruct[key] = NewTerminateReply()
}

// NewTerminateRequest is the default constructor for
// TerminateRequest
//
// returns *TerminateRequest -> pointer to a newly initialized
// TerminateReqeuest in memory
func NewTerminateRequest() *TerminateRequest {
	request := new(TerminateRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.TerminateRequest
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

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetRequestID gets a request id from a ProxyMessage's properties
//
// returns int64 -> long as a ProxyRequest's request id from the properties map
func (request *TerminateRequest) GetRequestID() int64 {
	return request.GetLongProperty("RequestId")
}

// SetRequestID sets a request id in a ProxyRequest's ProxyMessage
// properties
//
// param value int64 -> the long representation of a ProxyRequest's
// request id to be set in the properties map
func (request *TerminateRequest) SetRequestID(value int64) {
	request.SetLongProperty("RequestId", value)
}
