package cancel

import (
	"errors"
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// CancelReply is a ProxyReply of MessageType
	// CancelReply.  It holds a reference to a ProxyReply in memory
	CancelReply struct {
		*base.ProxyReply
	}
)

// NewCancelReply is the default constructor for
// a CancelReply
//
// returns *CancelReply -> a pointer to a newly initialized
// CancelReply in memory
func NewCancelReply() *CancelReply {
	reply := new(CancelReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.CancelReply
	return reply
}

// GetWasCancelled gets the WasCancelled property as a bool
// from a CancelReply's properties map
//
// returns bool -> a boolean from a CancelReply's properties map
// that indicates if an operation has been cancelled
func (reply *CancelReply) GetWasCancelled() bool {
	return reply.GetBoolProperty("WasCancelled")
}

// SetWasCancelled sets the WasCancelled property in a
// CancelReply's properties map
//
// param value bool -> the bool value to set as the WasCancelled
// property in a CancelReply's properties map
func (reply *CancelReply) SetWasCancelled(value bool) {
	reply.SetBoolProperty("WasCancelled", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *CancelReply) Clone() base.IProxyMessage {
	cancelReply := NewCancelReply()
	var messageClone base.IProxyMessage = cancelReply
	reply.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *CancelReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	v, ok := target.(*CancelReply)
	if ok {
		v.SetWasCancelled(reply.GetWasCancelled())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *CancelReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *CancelReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *CancelReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *CancelReply) GetRequestID() int64 {
	return reply.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *CancelReply) SetRequestID(value int64) {
	reply.SetLongProperty("RequestId", value)
}

// GetError inherits docs from ProxyReply.GetError()
func (reply *CancelReply) GetError() *string {
	return reply.GetStringProperty("Error")
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *CancelReply) SetError(value *string) {
	reply.SetStringProperty("Error", value)
}

// GetErrorDetails inherits docs from ProxyReply.GetErrorDetails()
func (reply *CancelReply) GetErrorDetails() *string {
	return reply.GetStringProperty("ErrorDetails")
}

// SetErrorDetails inherits docs from ProxyReply.SetErrorDetails()
func (reply *CancelReply) SetErrorDetails(value *string) {
	reply.SetStringProperty("ErrorDetails", value)
}

// GetErrorType inherits docs from ProxyReply.GetErrorType()
func (reply *CancelReply) GetErrorType() messages.CadenceErrorTypes {

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
func (reply *CancelReply) SetErrorType(value messages.CadenceErrorTypes) {
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
