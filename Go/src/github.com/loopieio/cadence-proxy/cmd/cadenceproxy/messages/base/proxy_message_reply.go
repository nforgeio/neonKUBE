package base

type (
	ProxyReply struct {
		*ProxyMessage
		RequestId    string
		ErrorType    string
		ErrorMessage string
	}

	IProxyReply interface {
	}
)

// GetLongProperty inherits docs from ProxyMessage.GetLongProperty()
func (reply *ProxyReply) GetLongProperty() int64 {
	return reply.ProxyMessage.GetLongProperty("RequestId")
}

// SetLongProperty inherits docs from ProxyMessage.SetLongProperty()
func (reply *ProxyReply) SetLongProperty(value int64) {
	reply.ProxyMessage.SetLongProperty("RequestId", value)
}
