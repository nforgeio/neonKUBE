package cluster

import (
	"errors"
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// DomainRegisterReply is a ProxyReply of MessageType
	// DomainRegisterReply.  It holds a reference to a ProxyReply in memory
	DomainRegisterReply struct {
		*base.ProxyReply
	}
)

// NewDomainRegisterReply is the default constructor for
// a DomainRegisterReply
//
// returns *DomainRegisterReply -> a pointer to a newly initialized
// DomainRegisterReply in memory
func NewDomainRegisterReply() *DomainRegisterReply {
	reply := new(DomainRegisterReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.DomainRegisterReply
	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *DomainRegisterReply) Clone() base.IProxyMessage {
	domainRegisterReply := NewDomainRegisterReply()
	var messageClone base.IProxyMessage = domainRegisterReply
	reply.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *DomainRegisterReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *DomainRegisterReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *DomainRegisterReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *DomainRegisterReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *DomainRegisterReply) GetRequestID() int64 {
	return reply.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *DomainRegisterReply) SetRequestID(value int64) {
	reply.SetLongProperty("RequestId", value)
}

// GetError inherits docs from ProxyReply.GetError()
func (reply *DomainRegisterReply) GetError() *string {
	return reply.GetStringProperty("Error")
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *DomainRegisterReply) SetError(value *string) {
	reply.SetStringProperty("Error", value)
}

// GetErrorDetails inherits docs from ProxyReply.GetErrorDetails()
func (reply *DomainRegisterReply) GetErrorDetails() *string {
	return reply.GetStringProperty("ErrorDetails")
}

// SetErrorDetails inherits docs from ProxyReply.SetErrorDetails()
func (reply *DomainRegisterReply) SetErrorDetails(value *string) {
	reply.SetStringProperty("ErrorDetails", value)
}

// GetErrorType inherits docs from ProxyReply.GetErrorType()
func (reply *DomainRegisterReply) GetErrorType() messages.CadenceErrorTypes {

	// Grap the pointer to the error string in the properties map
	errorStringPtr := reply.GetStringProperty("ErrorType")
	if errorStringPtr == nil {
		return messages.None
	}

	// dereference and switch block on the value
	errorString := *errorStringPtr
	switch errorString {
	case "cancelled":
		return messages.Cancelled
	case "custom":
		return messages.Custom
	case "generic":
		return messages.Generic
	case "panic":
		return messages.Panic
	case "terminated":
		return messages.Terminated
	case "timeout":
		return messages.Timeout
	default:
		err := errors.New("Not implemented exception")
		panic(err)
	}
}

// SetErrorType inherits docs from ProxyReply.SetErrorType()
func (reply *DomainRegisterReply) SetErrorType(value messages.CadenceErrorTypes) {
	var typeString string

	// switch block on the param value
	switch value {
	case messages.None:
		reply.Properties["ErrorType"] = nil
		return
	case messages.Cancelled:
		typeString = "cancelled"
		break
	case messages.Custom:
		typeString = "custom"
		break
	case messages.Generic:
		typeString = "generic"
		break
	case messages.Panic:
		typeString = "panic"
		break
	case messages.Terminated:
		typeString = "terminated"
		break
	case messages.Timeout:
		typeString = "timeout"
		break
	default:
		// panic if type is not recognized or implemented yet
		err := errors.New("Not implemented exception")
		panic(err)
	}

	// set the string in the properties map
	reply.SetStringProperty("ErrorType", &typeString)
}
