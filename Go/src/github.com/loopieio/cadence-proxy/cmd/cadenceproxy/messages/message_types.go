package messages

type MessageType int32

const (
	/// <summary>
	/// Indicates a message with an unspecified type.  This normally indicates an error.
	/// </summary>
	Unspecified MessageType = iota

	//---------------------------------------------------------------------
	// Global messages

	/// <summary>
	/// <b>library --> proxy:</b> Informs the proxy of the network endpoint where the
	/// library is listening for proxy messages.  The proxy should respond with an
	/// <see cref="InitializeReply"/> when it's ready to begin receiving inbound
	/// proxy messages.
	/// </summary>
	InitializeRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="InitializeRequest"/> message
	/// to indicate that the proxy ready to begin receiving inbound proxy messages.
	/// </summary>
	InitializeReply

	/// <summary>
	/// library --> proxy: Requests that the proxy establish a connection to a Cadence
	/// cluster.  This maps to a <c>NewClient()</c> in the proxy.
	/// </summary>
	ConnectRequest

	/// <summary>
	/// proxy --> library: Sent in response to a <see cref="ConnectRequest"/> message.
	/// </summary>
	ConnectReply

	/// <summary>
	/// <b>library --> proxy:</b> Signals the proxy that it should terminate gracefully.  The
	/// proxy should send a <see cref="TerminateReply"/> back to the library and
	/// then exit terminating the process.
	/// </summary>
	TerminateRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="TerminateRequest"/> message.
	/// </summary>
	TerminateReply

	/// <summary>
	/// <b>library --> proxy:</b> Requests that the proxy register a Cadence domain.
	/// </summary>
	DomainRegisterRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="DomainRegisterRequest"/> message.
	/// </summary>
	DomainRegisterReply

	/// <summary>
	/// <b>library --> proxy:</b> Requests that the proxy return the details for a Cadence domain.
	/// </summary>
	DomainDescribeRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="DomainDescribeRequest"/> message.
	/// </summary>
	DomainDescribeReply

	/// <summary>
	/// <b>library --> proxy:</b> Requests that the proxy update a Cadence domain.
	/// </summary>
	DomainUpdateRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="DomainUpdateRequest"/> message.
	/// </summary>
	DomainUpdateReply
)

const (
	//---------------------------------------------------------------------
	// Workflow messages
	//
	// Note that all workflow client request messages will include [WorkflowClientId] property
	// identifying the target workflow client.

	/// <summary>
	/// <b>library --> proxy:</b> Registers a workflow handler.
	/// </summary>
	Workflow_RegisterRequest MessageType = iota + 100

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_RegisterRequest"/> message.
	/// </summary>
	Workflow_RegisterflowReply

	/// <summary>
	/// <b>library --> proxy:</b> Starts a workflow.
	/// </summary>
	Workflow_StartWorkflowRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_StartWorkflowRequest"/> message.
	/// </summary>
	Workflow_StartWorkflowReply

	/// <summary>
	/// <b>library --> proxy:</b> Executes a workflow.
	/// </summary>
	Workflow_ExecuteWorkflowRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_ExecuteWorkflowRequest"/> message.
	/// </summary>
	Workflow_ExecuteWorkflowReply

	/// <summary>
	/// <b>library --> proxy:</b> Signals a workflow.
	/// </summary>
	Workflow_SignalWorkflowRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_SignalWorkflowRequest"/> message.
	/// </summary>
	Workflow_SignalWorkslowReply

	/// <summary>
	/// <b>library --> proxy:</b> Signals a workflow starting it if necessary.
	/// </summary>
	Workflow_SignalWorkflowWithStartRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_SignalWorkflowWithStartRequest"/> message.
	/// </summary>
	Workflow_SignalWorkflowWithStartReply

	/// <summary>
	/// <b>library --> proxy:</b> Cancels a workflow.
	/// </summary>
	Workflow_CancelWorkflowRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_CancelWorkflowRequest"/> message.
	/// </summary>
	Workflow_CancelWorkflowReply

	/// <summary>
	/// <b>library --> proxy:</b> Terminates a workflow.
	/// </summary>
	Workflow_TerminateWorkflowRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_TerminateWorkflowRequest"/> message.
	/// </summary>
	Workflow_TerminateWorkflowReply

	/// <summary>
	/// <b>library --> proxy:</b> Requests the a workflow's history.
	/// </summary>
	Workflow_GetWorkflowHistoryRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_GetWorkflowHistoryRequest"/> message.
	/// </summary>
	Workflow_GetWorkflowHistoryReply

	/// <summary>
	/// <b>library --> proxy:</b> Indicates that an activity has completed.
	/// </summary>
	Workflow_CompleteActivityRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_CompleteActivityRequest"/> message.
	/// </summary>
	Workflow_CompleteActivityReply

	/// <summary>
	/// <b>library --> proxy:</b> Indicates that the activity with a specified ID as completed has completed.
	/// </summary>
	Workflow_CompleteActivityByIdRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_CompleteActivityByIdRequest"/> message.
	/// </summary>
	Workflow_CompleteActivityByIdReply

	/// <summary>
	/// <b>library --> proxy:</b> Records an activity heartbeat.
	/// </summary>
	Workflow_RecordActivityHeartbeatRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_RecordActivityHeartbeatRequest"/> message.
	/// </summary>
	Workflow_RecordActivityHeartbeatReply

	/// <summary>
	/// <b>library --> proxy:</b> Records a heartbeat for an activity specified by ID.
	/// </summary>
	Workflow_RecordActivityHeartbeatByIdRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_RecordActivityHeartbeatByIdRequest"/> message.
	/// </summary>
	Workflow_RecordActivityHeartbeatByIdReply

	/// <summary>
	/// <b>library --> proxy:</b> Requests the list of closed workflows.
	/// </summary>
	Workflow_ListClosedWorkflowRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_ListClosedWorkflowRequest"/> message.
	/// </summary>
	Workflow_ListClosedWorkflowReply

	/// <summary>
	/// <b>library --> proxy:</b> Requests the list of open workflows.
	/// </summary>
	Workflow_ListOpenWorkflowRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_ListOpenWorkflowRequest"/> message.
	/// </summary>
	Workflow_ListOpenWorkflowReply

	/// <summary>
	/// <b>library --> proxy:</b> Queries a workflow's last execution.
	/// </summary>
	Workflow_QueryWorkflowRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_QueryWorkflowRequest"/> message.
	/// </summary>
	Workflow_QueryWorkflowReply

	/// <summary>
	/// <b>library --> proxy:</b> Returns information about a worflow execution.
	/// </summary>
	Workflow_DescribeWorkflowExecutionRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_DescribeWorkflowExecutionRequest"/> message.
	/// </summary>
	Workflow_DescribeWorkflowExecutionReply

	/// <summary>
	/// <b>RESERVED:</b> This is not currently implemented.
	/// </summary>
	Workflow_DescribeTaskListRequest

	/// <summary>
	/// <b>RESERVED:</b> This is not currently implemented.
	/// </summary>
	Workflow_DescribeTaskListReply

	/// <summary>
	/// <b>proxy --> library:</b> Commands the client library and associated .NET application
	/// to process a workflow instance.
	/// </summary>
	Workflow_InvokeRequest

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="Workflow_InvokeRequest"/> message.
	/// </summary>
	Workflow_InvokeReply

	/// <summary>
	/// <b>proxy --> library:</b> Initiates execution of a child workflow.
	/// </summary>
	Workflow_ExecuteChildRequest

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="Workflow_InvokeRequest"/> message.
	/// </summary>
	Workflow_ExecuteChildReply

	/// <summary>
	/// <b>library --> proxy:</b> Indicates that .NET application wishes to consume signals from
	/// a named channel.  Any signals received by the proxy will be forwarded to the
	/// library via <see cref="Workflow_SignalReceivedRequest"/> messages.
	/// </summary>
	Workflow_SignalSubscribeRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Workflow_SignalSubscribeRequest"/> message.
	/// </summary>
	Workflow_SignalSubscribeReply

	/// <summary>
	/// <b>proxy --> library:</b> Send when a signal is received by the proxy on a subscribed channel.
	/// </summary>
	Workflow_SignalReceivedRequest

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="Workflow_SignalReceivedRequest"/> message.
	/// </summary>
	Workflow_SignalReceivedReply

	/// <summary>
	/// <para>
	/// <b>proxy --> library:</b> Implements the standard Cadence <i>side effect</i> behavior by
	/// transmitting a <see cref="Workflow_SideEffectInvokeRequest"/> to the library and
	/// waiting for the <see cref="Workflow_SideEffectInvokeReply"/> reply persisting the
	/// answer in the workflow history and then transmitting the answer back to the .NET
	/// workflow implementation via a <see cref="Workflow_SideEffectReply"/>.
	/// </para>
	/// <para>
	/// This message includes a unique identifier that is used to ensure that a specific side effect
	/// operation results in only a single <see cref="Workflow_SideEffectInvokeRequest"/> message to
	/// the .NET workflow application per workflow instance.  Subsequent calls will simply return the
	/// value from the execution history.
	/// </para>
	/// </summary>
	Workflow_SideEffectRequest

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="Workflow_SignalReceivedRequest"/> message.
	/// </summary>
	Workflow_SideEffectReply

	/// <summary>
	/// <b>proxy --> library:</b> Sent by the proxy to the library the first time a side effect
	/// operation is submitted a workflow instance.  The library will response with the
	/// side effect value to be persisted in the workflow history and returned back to
	/// the the .NET workflow application.
	/// </summary>
	Workflow_SideEffectInvokeRequest

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="Workflow_SignalReceivedRequest"/> message.
	/// </summary>
	Workflow_SideEffectInvokeReply
)

const (
	//---------------------------------------------------------------------
	// Domain messages
	//
	// Note that all domain client request messages will include a [DomainClientId] property
	// identifying the target domain client.

	/// <summary>
	/// <b>library --> proxy:</b> Registers a Cadence domain.
	/// </summary>
	Domain_RegisterRequest MessageType = iota + 200

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Domain_RegisterRequest"/> message.
	/// </summary>
	Domain_RegisterReply

	/// <summary>
	/// <b>library --> proxy:</b> Describes a Cadence domain.
	/// </summary>
	Domain_DescribeRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Domain_DescribeRequest"/> message.
	/// </summary>
	Domain_DescribeReply

	/// <summary>
	/// <b>library --> proxy:</b> Updates a Cadence domain.
	/// </summary>
	Domain_UpdateRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Domain_UpdateRequest"/> message.
	/// </summary>
	Domain_UpdateReply
)

const (
	//---------------------------------------------------------------------
	// Activity messages

	/// <summary>
	/// <b>proxy --> library:</b> Commands the client library and associated .NET application
	/// to process an activity instance.
	/// </summary>
	Activity_InvokeRequest MessageType = iota + 300

	/// <summary>
	/// <b>library --> proxy:</b> Sent in response to a <see cref="Activity_InvokeRequest"/> message.
	/// </summary>
	Activity_InvokeReply

	/// <summary>
	/// <b>library --> proxy:</b> Requests the heartbeat details from the last failed attempt.
	/// </summary>
	Activity_GetHeartbeatDetailsRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Activity_GetHeartbeatDetailsRequest"/> message.
	/// </summary>
	Activity_GetHeartbeatDetailsReply

	/// <summary>
	/// <b>library --> proxy:</b> Logs a message for an activity.
	/// </summary>
	Activity_LogRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Activity_LogRequest"/> message.
	/// </summary>
	Activity_LogReply

	/// <summary>
	/// <b>library --> proxy:</b> Records a heartbeat message for an activity.
	/// </summary>
	Activity_RecordHeartbeatRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Activity_RecordHeartbeatRequest"/> message.
	/// </summary>
	Activity_RecordHeartbeatReply

	/// <summary>
	/// <b>library --> proxy:</b> Determines whether an activity execution has any heartbeat details.
	/// </summary>
	Activity_HasHeartbeatDetailsRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Activity_HasHeartbeatDetailsRequest"/> message.
	/// </summary>
	Activity_HasHeartbeatDetailsReply

	/// <summary>
	/// <b>library --> proxy:</b> Signals that the application executing an activity is terminating
	/// giving the the proxy a chance to gracefully inform Cadence and then terminate the activity.
	/// </summary>
	Activity_StopRequest

	/// <summary>
	/// <b>proxy --> library:</b> Sent in response to a <see cref="Activity_StopRequest"/> message.
	/// </summary>
	Activity_StopReply
)

func (t MessageType) String() string {
	return [...]string{
		"Unspecified",
		"InitializeRequest",
		"InitializeReply",
		"ConnectRequest",
		"ConnectReply",
		"TerminateRequest",
		"TerminateReply",
		"DomainRegisterRequest",
		"DomainRegisterReply",
		"DomainDescribeRequest",
		"DomainDescribeReply",
		"DomainUpdateRequest",
		"DomainUpdateReply",
		"Workflow_RegisterRequest",
		"Workflow_RegisterflowReply",
		"Workflow_StartWorkflowRequest",
		"Workflow_StartWorkflowReply",
		"Workflow_ExecuteWorkflowRequest",
		"Workflow_ExecuteWorkflowReply",
		"Workflow_SignalWorkflowRequest",
		"Workflow_SignalWorkslowReply",
		"Workflow_SignalWorkflowWithStartRequest",
		"Workflow_SignalWorkflowWithStartReply",
		"Workflow_CancelWorkflowRequest",
		"Workflow_CancelWorkflowReply",
		"Workflow_TerminateWorkflowRequest",
		"Workflow_TerminateWorkflowReply",
		"Workflow_GetWorkflowHistoryRequest",
		"Workflow_GetWorkflowHistoryReply",
		"Workflow_CompleteActivityRequest",
		"Workflow_CompleteActivityReply",
		"Workflow_CompleteActivityByIdRequest",
		"Workflow_CompleteActivityByIdReply",
		"Workflow_RecordActivityHeartbeatRequest",
		"Workflow_RecordActivityHeartbeatReply",
		"Workflow_RecordActivityHeartbeatByIdRequest",
		"Workflow_RecordActivityHeartbeatByIdReply",
		"Workflow_ListClosedWorkflowRequest",
		"Workflow_ListClosedWorkflowReply",
		"Workflow_ListOpenWorkflowRequest",
		"Workflow_ListOpenWorkflowReply",
		"Workflow_QueryWorkflowRequest",
		"Workflow_QueryWorkflowReply",
		"Workflow_DescribeWorkflowExecutionRequest",
		"Workflow_DescribeWorkflowExecutionReply",
		"Workflow_DescribeTaskListRequest",
		"Workflow_DescribeTaskListReply",
		"Workflow_InvokeRequest",
		"Workflow_InvokeReply",
		"Workflow_ExecuteChildRequest",
		"Workflow_ExecuteChildReply",
		"Workflow_SignalSubscribeRequest",
		"Workflow_SignalSubscribeReply",
		"Workflow_SignalReceivedRequest",
		"Workflow_SignalReceivedReply",
		"Workflow_SideEffectRequest",
		"Workflow_SideEffectReply",
		"Workflow_SideEffectInvokeRequest",
		"Workflow_SideEffectInvokeReply",
		"Domain_RegisterRequest",
		"Domain_RegisterReply",
		"Domain_DescribeRequest",
		"Domain_DescribeReply",
		"Domain_UpdateRequest",
		"Domain_UpdateReply",
		"Activity_InvokeRequest",
		"Activity_InvokeReply",
		"Activity_GetHeartbeatDetailsRequest",
		"Activity_GetHeartbeatDetailsReply",
		"Activity_LogRequest",
		"Activity_LogReply",
		"Activity_RecordHeartbeatRequest",
		"Activity_RecordHeartbeatReply",
		"Activity_HasHeartbeatDetailsRequest",
		"Activity_HasHeartbeatDetailsReply",
		"Activity_StopRequest",
		"Activity_StopReply"}[t]
}
