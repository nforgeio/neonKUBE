package cadence

// CadenceErrorTypes is an enumerated list of
// all of the cadence error types
type ErrorTypes int

const (
	// None indicates no errors
	None ErrorTypes = iota

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

func (t ErrorTypes) String() string {
	return [...]string{
		"None",
		"Cancelled",
		"Custom",
		"Generic",
		"Panic",
		"Terminated",
		"Timeout",
	}[t]
}
