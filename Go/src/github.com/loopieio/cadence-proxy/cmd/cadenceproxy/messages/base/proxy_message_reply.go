package base

type (
	ProxyReply struct {
		*ProxyMessage
		RequestID    string
		ErrorType    string
		ErrorMessage string
	}
)
