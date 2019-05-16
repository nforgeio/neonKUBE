package messagetypes

// MessageType is an enumerated mapping
// of all ProxyMessage types
type MessageType int32

const (
	/// <summary>
	/// Indicates a message with an unspecified type.  This normally indicates an error.
	/// </summary>
	Unspecified MessageType = 0

	//---------------------------------------------------------------------
	// Global messages

	/// <summary>
	/// <b>library --> proxy:</b> Informs the proxy of the network endpoint where the
	/// library is listening for proxy messages.  The proxy should respond with an
	/// <see cref="InitializeReply"/> when it's ready to begin receiving inbound
	/// proxy messages.
	/// </summary>
	InitializeRequest MessageType = 1

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="InitializeRequest"/> message
	/// to indicate that the proxy ready to begin receiving inbound proxy messages.
	/// </summary>
	InitializeReply MessageType = 2

	/// <summary>
	/// library --> proxy: Requests that the proxy establish a connection to a Cadence
	/// cluster.  This maps to a <c>NewClient()</c> in the proxy.
	/// </summary>
	ConnectRequest MessageType = 3

	/// <summary>
	/// proxy --> library: Sent in response to a <see cref="ConnectRequest"/> message.
	/// </summary>
	ConnectReply MessageType = 4

	/// <summary>
	/// <b>library --> proxy:</b> Signals the proxy that it should terminate gracefully.  The
	/// proxy should send a <see cref="TerminateReply"/> back to the library and
	/// then exit terminating the process.
	/// </summary>
	TerminateRequest MessageType = 5

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="TerminateRequest"/> message.
	/// </summary>
	TerminateReply MessageType = 6

	/// <summary>
	/// <b>library --> proxy:</b> Requests that the proxy register a Cadence domain.
	/// </summary>
	DomainRegisterRequest MessageType = 7

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="DomainRegisterRequest"/> message.
	/// </summary>
	DomainRegisterReply MessageType = 8

	/// <summary>
	/// <b>library --> proxy:</b> Requests that the proxy return the details for a Cadence domain.
	/// </summary>
	DomainDescribeRequest MessageType = 9

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="DomainDescribeRequest"/> message.
	/// </summary>
	DomainDescribeReply MessageType = 10

	/// <summary>
	/// <b>library --> proxy:</b> Requests that the proxy update a Cadence domain.
	/// </summary>
	DomainUpdateRequest MessageType = 11

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="DomainUpdateRequest"/> message.
	/// </summary>
	DomainUpdateReply MessageType = 12

	/// <summary>
	/// <b>library --> proxy:</b> Sent periodically (every second) by the library to the
	/// proxy to verify that it is still healthy.
	/// </summary>
	HeartbeatRequest MessageType = 13

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="HeartbeatRequest"/> message.
	/// </summary>
	HeartbeatReply MessageType = 14

	/// <summary>
	/// <b>library --> proxy:</b> Sent to request that a pending operation be cancelled.
	/// </summary>
	CancelRequest MessageType = 15

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="CancelRequest"/> message
	/// indicating that the operation was canceled or that it already completed or no longer
	/// exists.
	/// </summary>
	CancelReply MessageType = 16

	//---------------------------------------------------------------------
	// Workflow messages
	//
	// Note that all workflow client request messages will include [WorkflowClientId] property
	// identifying the target workflow client.

	/// <summary>
	/// <b>library --> proxy:</b> Registers a workflow handler.
	/// </summary>
	WorkflowRegisterRequest MessageType = 100

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowRegisterRequest"/> message.
	/// </summary>
	WorkflowRegisterReply MessageType = 101

	/// <summary>
	/// <b>library --> proxy:</b> Starts a workflow.
	/// </summary>
	WorkflowExecuteRequest MessageType = 102

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowExecuteRequest"/> message.
	/// </summary>
	WorkflowExecuteReply MessageType = 103

	/// <summary>
	/// <b>library --> proxy:</b> Signals a workflow.
	/// </summary>
	WorkflowSignalWorkflowRequest MessageType = 104

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowSignalWorkflowRequest"/> message.
	/// </summary>
	WorkflowSignalReply MessageType = 105

	/// <summary>
	/// <b>library --> proxy:</b> Signals a workflow starting it if necessary.
	/// </summary>
	WorkflowSignalWithStartRequest MessageType = 106

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowSignalWithStartRequest"/> message.
	/// </summary>
	WorkflowSignalWithStartReply MessageType = 107

	/// <summary>
	/// <b>library --> proxy:</b> Cancels a workflow.
	/// </summary>
	WorkflowCancelRequest MessageType = 108

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowCancelRequest"/> message.
	/// </summary>
	WorkflowCancelReply MessageType = 109

	/// <summary>
	/// <b>library --> proxy:</b> Terminates a workflow.
	/// </summary>
	WorkflowTerminateRequest MessageType = 110

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowTerminateRequest"/> message.
	/// </summary>
	WorkflowTerminateReply MessageType = 111

	/// <summary>
	/// <b>library --> proxy:</b> Requests the a workflow's history.
	/// </summary>
	WorkflowGetHistoryRequest MessageType = 112

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowGetHistoryRequest"/> message.
	/// </summary>
	WorkflowGetWorkflowHistoryReply MessageType = 113

	/// <summary>
	/// <b>library --> proxy:</b> Indicates that an activity has completed.
	/// </summary>
	WorkflowCompleteActivityRequest MessageType = 114

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowCompleteActivityRequest"/> message.
	/// </summary>
	WorkflowCompleteActivityReply MessageType = 115

	/// <summary>
	/// <b>library --> proxy:</b> Indicates that the activity with a specified ID as completed has completed.
	/// </summary>
	WorkflowCompleteActivityByIdRequest MessageType = 116

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowCompleteActivityByIdRequest"/> message.
	/// </summary>
	WorkflowCompleteActivityByIdReply MessageType = 117

	/// <summary>
	/// <b>library --> proxy:</b> Records an activity heartbeat.
	/// </summary>
	WorkflowRecordActivityHeartbeatRequest MessageType = 118

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowRecordActivityHeartbeatRequest"/> message.
	/// </summary>
	WorkflowRecordActivityHeartbeatReply MessageType = 119

	/// <summary>
	/// <b>library --> proxy:</b> Records a heartbeat for an activity specified by ID.
	/// </summary>
	WorkflowRecordActivityHeartbeatByIdRequest MessageType = 120

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowRecordActivityHeartbeatByIdRequest"/> message.
	/// </summary>
	WorkflowRecordActivityHeartbeatByIdReply MessageType = 121

	/// <summary>
	/// <b>library --> proxy:</b> Requests the list of closed workflows.
	/// </summary>
	WorkflowListClosedRequest MessageType = 122

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowListClosedRequest"/> message.
	/// </summary>
	WorkflowListClosedReply MessageType = 123

	/// <summary>
	/// <b>library --> proxy:</b> Requests the list of open workflows.
	/// </summary>
	WorkflowListOpenWorkflowRequest MessageType = 124

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowListOpenWorkflowRequest"/> message.
	/// </summary>
	WorkflowListOpenReply MessageType = 125

	/// <summary>
	/// <b>library --> proxy:</b> Queries a workflow's last execution.
	/// </summary>
	WorkflowQueryRequest MessageType = 126

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowQueryRequest"/> message.
	/// </summary>
	WorkflowQueryReply MessageType = 127

	/// <summary>
	/// <b>library --> proxy:</b> Returns information about a worflow execution.
	/// </summary>
	WorkflowDescribeWorkflowExecutionRequest MessageType = 128

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowDescribeWorkflowExecutionRequest"/> message.
	/// </summary>
	WorkflowDescribexecutionReply MessageType = 129

	/// <summary>
	/// <b>RESERVED:</b> This is not currently implemented.
	/// </summary>
	WorkflowDescribeTaskListRequest MessageType = 130

	/// <summary>
	/// <b>RESERVED:</b> This is not currently implemented.
	/// </summary>
	WorkflowDescribeTaskListReply MessageType = 131

	/// <summary>
	/// <b>proxy --> library:</b> Commands the client library and associated .NET application
	/// to process a workflow instance.
	/// </summary>
	WorkflowInvokeRequest MessageType = 132

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="WorkflowInvokeRequest"/> message.
	/// </summary>
	WorkflowInvokeReply MessageType = 133

	/// <summary>
	/// <b>proxy --> library:</b> Initiates execution of a child workflow.
	/// </summary>
	WorkflowExecuteChildRequest MessageType = 134

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="WorkflowInvokeRequest"/> message.
	/// </summary>
	WorkflowExecuteChildReply MessageType = 135

	/// <summary>
	/// <b>library --> proxy:</b> Indicates that .NET application wishes to consume signals from
	/// a named channel.  Any signals received by the proxy will be forwarded to the
	/// library via <see cref="WorkflowSignalReceivedRequest"/> messages.
	/// </summary>
	WorkflowSignalSubscribeRequest MessageType = 136

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="WorkflowSignalSubscribeRequest"/> message.
	/// </summary>
	WorkflowSignalSubscribeReply MessageType = 137

	/// <summary>
	/// <b>proxy --> library:</b> Send when a signal is received by the proxy on a subscribed channel.
	/// </summary>
	WorkflowSignalReceivedRequest MessageType = 138

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="WorkflowSignalReceivedRequest"/> message.
	/// </summary>
	WorkflowSignalReceivedReply MessageType = 139

	/// <summary>
	/// <para>
	/// <b>proxy --> library:</b> Implements the standard Cadence <i>side effect</i> behavior by
	/// transmitting a <see cref="WorkflowSideEffectInvokeRequest"/> to the library and
	/// waiting for the <see cref="WorkflowSideEffectInvokeReply"/> reply persisting the
	/// answer in the workflow history and then transmitting the answer back to the .NET
	/// workflow implementation via a <see cref="WorkflowSideEffectReply"/>.
	/// </para>
	/// <para>
	/// This message includes a unique identifier that is used to ensure that a specific side effect
	/// operation results in only a single <see cref="WorkflowSideEffectInvokeRequest"/> message to
	/// the .NET workflow application per workflow instance.  Subsequent calls will simply return the
	/// value from the execution history.
	/// </para>
	/// </summary>
	WorkflowSideEffectRequest MessageType = 140

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="WorkflowSignalReceivedRequest"/> message.
	/// </summary>
	WorkflowSideEffectReply MessageType = 141

	/// <summary>
	/// <b>proxy --> library:</b> Sent by the proxy to the library the first time a side effect
	/// operation is submitted a workflow instance.  The library will response with the
	/// side effect value to be persisted in the workflow history and returned back to
	/// the the .NET workflow application.
	/// </summary>
	WorkflowSideEffectInvokeRequest MessageType = 142

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="WorkflowSignalReceivedRequest"/> message.
	/// </summary>
	WorkflowSideEffectInvokeReply MessageType = 143

	//---------------------------------------------------------------------
	// Activity messages

	/// <summary>
	/// <b>proxy --> library:</b> Commands the client library and associated .NET application
	/// to process an activity instance.
	/// </summary>
	ActivityInvokeRequest MessageType = 200

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="ActivityInvokeRequest"/> message.
	/// </summary>
	ActivityInvokeReply MessageType = 201

	/// <summary>
	/// <b>library --> proxy:</b> Requests the heartbeat details from the last failed attempt.
	/// </summary>
	ActivityGetHeartbeatDetailsRequest MessageType = 202

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="ActivityGetHeartbeatDetailsRequest"/> message.
	/// </summary>
	ActivityGetHeartbeatDetailsReply MessageType = 203

	/// <summary>
	/// <b>library --> proxy:</b> Logs a message for an activity.
	/// </summary>
	ActivityLogRequest MessageType = 204

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="ActivityLogRequest"/> message.
	/// </summary>
	ActivityLogReply MessageType = 205

	/// <summary>
	/// <b>library --> proxy:</b> Records a heartbeat message for an activity.
	/// </summary>
	ActivityRecordHeartbeatRequest MessageType = 206

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="ActivityRecordHeartbeatRequest"/> message.
	/// </summary>
	ActivityRecordHeartbeatReply MessageType = 207

	/// <summary>
	/// <b>library --> proxy:</b> Determines whether an activity execution has any heartbeat details.
	/// </summary>
	ActivityHasHeartbeatDetailsRequest MessageType = 208

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="ActivityHasHeartbeatDetailsRequest"/> message.
	/// </summary>
	ActivityHasHeartbeatDetailsReply MessageType = 209

	/// <summary>
	/// <b>library --> proxy:</b> Signals that the application executing an activity is terminating
	/// giving the the proxy a chance to gracefully inform Cadence and then terminate the activity.
	/// </summary>
	ActivityStopRequest MessageType = 210

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="ActivityStopRequest"/> message.
	/// </summary>
	ActivityStopReply MessageType = 211
)
