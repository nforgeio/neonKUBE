package base

type (
	ProxyRequest struct {
		*ProxyMessage
		RequestID string
	}
)

func (pr *ProxyRequest) GetLongProperty() int64 {
	return pr.ProxyMessage.GetLongProperty("RequestId")
}

func (pr *ProxyRequest) SetLongProperty(value int64) {
	pr.ProxyMessage.SetLongProperty("RequestId", value)
}

func (pr *ProxyRequest) Clone() *IProxyMessage {
	proxyRequest := ProxyRequest{}
	var ipmClone IProxyMessage = &proxyRequest
	pr.CopyTo(&ipmClone)

	return &ipmClone
}

// Implementation not finished yet
func (pr *ProxyRequest) CopyTo(target *IProxyMessage) {
	pr.ProxyMessage.CopyTo(target)
	var ipm IProxyMessage = *target
	v, ok := ipm.(*ProxyRequest)
	if ok {
		v.RequestID = pr.RequestID
		//target = v
	}
}
