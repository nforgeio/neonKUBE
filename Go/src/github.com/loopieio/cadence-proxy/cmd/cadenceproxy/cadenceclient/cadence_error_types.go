package cadenceclient

// CadenceErrorTypes is an enumerated list of
// all of the cadence error types
type CadenceErrorTypes int

const (
	// None indicates no errors
	None CadenceErrorTypes = iota

	// Cancelled indicates that an operation was cancelled
	Cancelled

	// Custom is a custom error
	Custom

	// Generic is a generic error
	Generic

	// Panic is a panic error
	Panic

	// Terminated is a termination error
	Terminated

	// Timeout is a timeout error
	Timeout
)

func (t CadenceErrorTypes) String() string {
	return [...]string{
		"none",
		"cancelled",
		"custom",
		"generic",
		"panic",
		"terminated",
		"timeout",
	}[t]
}
