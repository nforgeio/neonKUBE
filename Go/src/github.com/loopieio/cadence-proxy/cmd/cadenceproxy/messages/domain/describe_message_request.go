package domain

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// DomainDescribeRequest is ProxyRequest of MessageType
	// DomainDescribeRequest.
	//
	// A DomainDescribeRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	DomainDescribeRequest struct {
		*base.ProxyRequest
		ReplyType messages.MessageType
	}
)

// InitDomainDescribe is a method that adds a key/value entry into the
// IntToMessageStruct at keys DomainDescribeRequest and DomainDescribeReply.
// The values are new instances of a DomainDescribeRequest and DomainDescribeReply
func InitDomainDescribe() {
	key := int(messages.DomainDescribeRequest)
	base.IntToMessageStruct[key] = NewDomainDescribeRequest()

	key = int(messages.DomainDescribeReply)
	base.IntToMessageStruct[key] = NewDomainDescribeReply()
}

// NewDomainDescribeRequest is the default constructor for a DomainDescribeRequest
//
// returns *DomainDescribeRequest -> a reference to a newly initialized
// DomainDescribeRequest in memory
func NewDomainDescribeRequest() *DomainDescribeRequest {
	request := new(DomainDescribeRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.DomainDescribeRequest
	request.ReplyType = messages.DomainDescribeReply
	return request
}

// GetName gets a DomainDescribeRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainDescribeRequest's Name
func (request *DomainDescribeRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a DomainDescribeRequest's TargetRequestId value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainDescribeRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *DomainDescribeRequest) Clone() base.IProxyMessage {
	domainDescribeRequest := NewDomainDescribeRequest()

	var messageClone base.IProxyMessage = domainDescribeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *DomainDescribeRequest) CopyTo(target base.IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	v, ok := target.(*DomainDescribeRequest)
	if ok {
		v.SetName(request.GetName())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *DomainDescribeRequest) SetProxyMessage(value *base.ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *DomainDescribeRequest) GetProxyMessage() *base.ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *DomainDescribeRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyRequest.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *DomainDescribeRequest) GetRequestID() int64 {
	return request.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *DomainDescribeRequest) SetRequestID(value int64) {
	request.SetLongProperty("RequestId", value)
}

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *DomainDescribeRequest) GetReplyType() messages.MessageType {
	return request.ReplyType
}
