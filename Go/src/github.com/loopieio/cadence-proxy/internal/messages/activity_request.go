package messages

type (
	ActivityRequest struct {
		*ProxyRequest
		ContextID string
	}
)
