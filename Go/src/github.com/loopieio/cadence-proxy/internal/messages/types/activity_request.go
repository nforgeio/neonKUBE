package types

type (
	ActivityRequest struct {
		*ProxyRequest
		ContextID string
	}
)
