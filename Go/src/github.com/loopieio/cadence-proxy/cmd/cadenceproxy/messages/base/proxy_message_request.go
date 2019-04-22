package base

import "log"

type (

	// ProxyRequest "extends" ProxyMessage and it is
	// a type of ProxyMessage that comes into the server
	// i.e. a request
	//
	// A ProxyRequest contains a RequestId and a reference to a
	// ProxyMessage struct
	ProxyRequest struct {

		// ProxyMessage is a reference to a ProxyMessage in memory
		*ProxyMessage

		// RequestId is the unique id of the ProxyRequest
		RequestId int64
	}
)

// GetLongProperty inherits docs from ProxyMessage.GetLongProperty()
func (request *ProxyRequest) GetLongProperty(key string) int64 {
	return request.ProxyMessage.GetLongProperty(key)
}

// SetLongProperty inherits docs from ProxyMessage.SetLongProperty()
func (request *ProxyRequest) SetLongProperty(key string, value int64) {
	request.ProxyMessage.SetLongProperty(key, value)
}

func (request *ProxyRequest) SetIProxyMessageProxyMessage(value *ProxyMessage) {
	*request.ProxyMessage = *value
}

// SetRequestId inherits docs from ProxyMessage.SetRequestId()
func (request *ProxyRequest) SetIProxyMessageRequestId(value int64) {
	request.RequestId = value
}

// Clone inherits docs from ProxyMessage.Clone()
func (request *ProxyRequest) Clone() IProxyMessage {
	proxyRequest := ProxyRequest{
		ProxyMessage: new(ProxyMessage),
	}

	var messageClone IProxyMessage = &proxyRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *ProxyRequest) CopyTo(target IProxyMessage) {
	request.ProxyMessage.CopyTo(target)
	target.SetIProxyMessageRequestId(request.RequestId)
}

// String inherits docs from ProxyMessage.String()
func (request *ProxyRequest) String() {
	log.Print("{\n")
	log.Println()
	log.Printf("\tRequestId: %d\n", request.RequestId)
	request.ProxyMessage.String()
	log.Print("}\n\n")
}
