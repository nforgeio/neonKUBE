package cluster

import (
	"errors"
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceerrors"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// InitializeReply is a ProxyReply of MessageType
	// InitializeReply.  It holds a reference to a ProxyReply in memory
	InitializeReply struct {
		*base.ProxyReply
	}
)

// NewInitializeReply is the default constructor for
// a InitializeReply
//
// returns *InitializeReply -> a pointer to a newly initialized
// InitializeReply in memory
func NewInitializeReply() *InitializeReply {
	reply := new(InitializeReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.InitializeReply
	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *InitializeReply) Clone() base.IProxyMessage {
	initializeReply := NewInitializeReply()
	var messageClone base.IProxyMessage = initializeReply
	reply.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *InitializeReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *InitializeReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *InitializeReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *InitializeReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *InitializeReply) GetRequestID() int64 {
	return reply.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *InitializeReply) SetRequestID(value int64) {
	reply.SetLongProperty("RequestId", value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *InitializeReply) GetError() *string {
	return reply.GetStringProperty("Error")
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *InitializeReply) SetError(value *string) {
	reply.SetStringProperty("Error", value)
}

// GetErrorDetails inherits docs from ProxyReply.GetErrorDetails()
func (reply *InitializeReply) GetErrorDetails() *string {
	return reply.GetStringProperty("ErrorDetails")
}

// SetErrorDetails inherits docs from ProxyReply.SetErrorDetails()
func (reply *InitializeReply) SetErrorDetails(value *string) {
	reply.SetStringProperty("ErrorDetails", value)
}

// GetErrorType inherits docs from ProxyReply.GetErrorType()
func (reply *InitializeReply) GetErrorType() cadenceerrors.CadenceErrorTypes {

	// Grap the pointer to the error string in the properties map
	errorStringPtr := reply.GetStringProperty("ErrorType")
	if errorStringPtr == nil {
		return cadenceerrors.None
	}

	// dereference and switch block on the value
	errorString := *errorStringPtr
	switch errorString {
	case "cancelled":
		return cadenceerrors.Cancelled
	case "custom":
		return cadenceerrors.Custom
	case "generic":
		return cadenceerrors.Generic
	case "panic":
		return cadenceerrors.Panic
	case "terminated":
		return cadenceerrors.Terminated
	case "timeout":
		return cadenceerrors.Timeout
	default:
		err := errors.New("not implemented exception")
		panic(err)
	}
}

// SetErrorType inherits docs from ProxyReply.SetErrorType()
func (reply *InitializeReply) SetErrorType(value cadenceerrors.CadenceErrorTypes) {
	var typeString string

	// switch block on the param value
	switch value {
	case cadenceerrors.None:
		reply.Properties["ErrorType"] = nil
		return
	case cadenceerrors.Cancelled:
		typeString = "cancelled"
	case cadenceerrors.Custom:
		typeString = "custom"
	case cadenceerrors.Generic:
		typeString = "generic"
	case cadenceerrors.Panic:
		typeString = "panic"
	case cadenceerrors.Terminated:
		typeString = "terminated"
	case cadenceerrors.Timeout:
		typeString = "timeout"
	default:
		// panic if type is not recognized or implemented yet
		err := errors.New("not implemented exception")
		panic(err)
	}

	// set the string in the properties map
	reply.SetStringProperty("ErrorType", &typeString)
}
