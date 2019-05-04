package cluster

import (
	"errors"
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// HeartbeatReply is a ProxyReply of MessageType
	// HeartbeatReply It holds a reference to a
	// ProxyReply in memory
	HeartbeatReply struct {
		*base.ProxyReply
	}
)

// NewHeartbeatReply is the default constructor for
// a HeartbeatReply
//
// returns *HeartbeatReply -> pointer to a newly initialized
// HeartbeatReply in memory
func NewHeartbeatReply() *HeartbeatReply {
	reply := new(HeartbeatReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.HeartbeatReply
	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *HeartbeatReply) Clone() base.IProxyMessage {
	heartbeatReply := NewHeartbeatReply()
	var messageClone base.IProxyMessage = heartbeatReply
	reply.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *HeartbeatReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *HeartbeatReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *HeartbeatReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *HeartbeatReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *HeartbeatReply) GetRequestID() int64 {
	return reply.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *HeartbeatReply) SetRequestID(value int64) {
	reply.SetLongProperty("RequestId", value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *HeartbeatReply) GetError() *string {
	return reply.GetStringProperty("Error")
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *HeartbeatReply) SetError(value *string) {
	reply.SetStringProperty("Error", value)
}

// GetErrorDetails inherits docs from ProxyReply.GetErrorDetails()
func (reply *HeartbeatReply) GetErrorDetails() *string {
	return reply.GetStringProperty("ErrorDetails")
}

// SetErrorDetails inherits docs from ProxyReply.SetErrorDetails()
func (reply *HeartbeatReply) SetErrorDetails(value *string) {
	reply.SetStringProperty("ErrorDetails", value)
}

// GetErrorType inherits docs from ProxyReply.GetErrorType()
func (reply *HeartbeatReply) GetErrorType() messages.CadenceErrorTypes {

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
		err := errors.New("not implemented exception")
		panic(err)
	}
}

// SetErrorType inherits docs from ProxyReply.SetErrorType()
func (reply *HeartbeatReply) SetErrorType(value messages.CadenceErrorTypes) {
	var typeString string

	// switch block on the param value
	switch value {
	case messages.None:
		reply.Properties["ErrorType"] = nil
		return
	case messages.Cancelled:
		typeString = "cancelled"
	case messages.Custom:
		typeString = "custom"
	case messages.Generic:
		typeString = "generic"
	case messages.Panic:
		typeString = "panic"
	case messages.Terminated:
		typeString = "terminated"
	case messages.Timeout:
		typeString = "timeout"
	default:
		// panic if type is not recognized or implemented yet
		err := errors.New("not implemented exception")
		panic(err)
	}

	// set the string in the properties map
	reply.SetStringProperty("ErrorType", &typeString)
}
