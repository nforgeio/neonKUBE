package base

type (
	ProxyRequest struct {
		*ProxyMessage
		RequestID string
	}
)
