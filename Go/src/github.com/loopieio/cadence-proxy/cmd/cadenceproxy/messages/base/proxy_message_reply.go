package base

import (
	"errors"

	cadenceclient "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadence"
)

type (
	ProxyReply struct {
		*ProxyMessage
		RequestId    string
		ErrorType    cadenceclient.CadenceErrorTypes
		ErrorMessage string
	}

	IProxyReply interface {
	}
)

// GetRequestID gets a request id from a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) GetRequestID() int64 {
	return reply.ProxyMessage.GetLongProperty(RequestIDKey)
}

// SetRequestID sets the request id in a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) SetRequestID(value int64) {
	reply.ProxyMessage.SetLongProperty(RequestIDKey, value)
}

// GetErrorMessage gets an error message from a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) GetErrorMessage() *string {
	return reply.ProxyMessage.GetStringProperty(CadenceErrorTypesKey)
}

// SetErrorMessage sets an error message in a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) SetErrorMessage(value *string) {
	reply.ProxyMessage.SetStringProperty(CadenceErrorTypesKey, value)
}

// GetErrorType gets the CadenceErrorType as a string
// from a ProxyReply's ProxyMessage properties
// and returns the corresponding CadenceErrorTypes
func (reply *ProxyReply) GetErrorType(errorType string) cadenceclient.CadenceErrorTypes {
	errorStringPtr := reply.ProxyMessage.GetStringProperty(CadenceErrorTypesKey)
	if errorStringPtr == nil {
		return cadenceclient.None
	}

	errorString := *errorStringPtr
	switch errorString {
	case "cancelled":
		return cadenceclient.Cancelled
	case "custom":
		return cadenceclient.Custom
	case "generic":
		return cadenceclient.Generic
	case "panic":
		return cadenceclient.Panic
	case "terminated":
		return cadenceclient.Terminated
	case "timeout":
		return cadenceclient.Timeout
	default:
		err := errors.New("Not implemented exception")
		panic(err)
	}
}

// SetErrorType sets the string representation of a CadenceErrorTypes
// in a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) SetErrorType(value cadenceclient.CadenceErrorTypes) {
	var typeString string
	switch value {
	case cadenceclient.None:
		reply.ProxyMessage.Properties[CadenceErrorTypesKey] = nil
		return
	case cadenceclient.Cancelled:
		typeString = "cancelled"
		break
	case cadenceclient.Custom:
		typeString = "custom"
		break
	case cadenceclient.Generic:
		typeString = "generic"
		break
	case cadenceclient.Panic:
		typeString = "panic"
		break
	case cadenceclient.Terminated:
		typeString = "terminated"
		break
	case cadenceclient.Timeout:
		typeString = "timeout"
		break
	default:
		err := errors.New("Not implemented exception")
		panic(err)
	}

	reply.ProxyMessage.SetStringProperty(CadenceErrorTypesKey, &typeString)
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ProxyReply) Clone() IProxyMessage {
	proxyReply := ProxyReply{
		ProxyMessage: new(ProxyMessage),
	}

	var messageClone IProxyMessage = &proxyReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ProxyReply) CopyTo(target IProxyMessage) {
	reply.ProxyMessage.CopyTo(target)
	v, ok := target.(*ProxyReply)
	if ok {
		v.RequestId = reply.RequestId
		*v.ProxyMessage = *reply.ProxyMessage
		v.ErrorType = reply.ErrorType
		v.ErrorMessage = reply.ErrorMessage
	}
}
