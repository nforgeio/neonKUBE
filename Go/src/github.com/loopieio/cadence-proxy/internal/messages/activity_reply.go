package messages

type (
	ActivityReply struct {
		*ProxyReply
		ContextID string
	}
)
