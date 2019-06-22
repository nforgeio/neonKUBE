//-----------------------------------------------------------------------------
// FILE:		helper.go
// CONTRIBUTOR: John C Burns
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

package cadenceclient

import (
	"context"
	"os"

	"go.uber.org/cadence/.gen/go/cadence/workflowserviceclient"
	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/encoded"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"
)

type (

	// CadenceClientHelper holds configuration details for building
	// the cadence domain client and the cadence workflow client
	// This is used for creating, update, and registering cadence domains
	// and stoping/starting cadence workflows workers
	//
	// Holds:
	// Workflowserviceclient.Interface -> Interface is a client for the WorkflowService service
	// CadenceClientConfiguration -> configuration information for building the cadence workflow and domain clients
	// *zap.Logger -> reference to a zap.Logger to log cadence client output to the console
	// *WorkflowClientBuilder -> reference to a WorkflowClientBuilder used to build the cadence
	// domain and workflow clients
	// *RegisterDomainRequest -> reference to a RegisterDomainRequest that contains configuration details
	// for registering a cadence domain
	CadenceClientHelper struct {
		Service workflowserviceclient.Interface
		Config  cadenceClientConfiguration
		Logger  *zap.Logger
		Builder *WorkflowClientBuilder
	}

	// CadenceClientConfiguration contains configuration details for
	// building the cadence workflow and domain clients as well as
	// configuration options for building rpc channels
	cadenceClientConfiguration struct {
		hostPort      string
		clientOptions *client.Options
	}
)

// domainCreated is a flag that prevents a CadenceClientHelper from
// creating a new domain client and registering an existing domain
// var domainCreated bool

// NewCadenceClientHelper is the default constructor
// for a new CadenceClientHelper
//
// returns *CadenceClientHelper -> pointer to a newly created
// CadenceClientHelper in memory
func NewCadenceClientHelper() *CadenceClientHelper {
	return new(CadenceClientHelper)
}

//----------------------------------------------------------------------------------
// CadenceClientHelper instance methods

// GetHostPort gets the hostPort from a CadenceClientHelper.Config
//
// returns string -> the hostPort string from a CadenceClientHelper.Config
func (helper *CadenceClientHelper) GetHostPort() string {
	return helper.Config.hostPort
}

// SetHostPort sets the hostPort in a CadenceClientHelper.Config
//
// param value string -> the string value to set as the hostPort in
// a CadenceClientHelper.Config
func (helper *CadenceClientHelper) SetHostPort(value string) {
	helper.Config.hostPort = value
}

// GetClientOptions gets the client.Options from a CadenceClientHelper.Config
//
// returns *client.ClientOptions -> a pointer to a client.Options instance
// in a CadenceClientHelper.Config
func (helper *CadenceClientHelper) GetClientOptions() *client.Options {
	return helper.Config.clientOptions
}

// SetClientOptions sets the client.Options in a CadenceClientHelper.Config
//
// param value *client.Options -> client.Options pointer in memory to set
// in CadenceClientHelper.Config
func (helper *CadenceClientHelper) SetClientOptions(value *client.Options) {
	helper.Config.clientOptions = value
}

// SetupServiceConfig configures a CadenceClientHelper's workflowserviceclient.Interface
// Service.  It also sets the Logger, the WorkflowClientBuilder, and acts as a helper for
// creating new cadence workflow and domain clients
//
// returns error -> error if there were any problems configuring
// or building the service client
func (helper *CadenceClientHelper) SetupServiceConfig() error {

	// exit if the service has already been setup
	if helper.Service != nil {
		return nil
	}

	// set the logger to global logger
	helper.Logger = zap.L()

	// Configure the CadenceClientHelper.Builder
	helper.Builder = NewBuilder(helper.Logger).
		SetHostPort(helper.Config.hostPort).
		SetClientOptions(helper.Config.clientOptions)

	// Configure the CadenceClientHelper.Service from the
	// CadenceClientHelper.Builder
	service, err := helper.Builder.BuildServiceClient()
	if err != nil {
		return err
	}
	helper.Service = service

	return nil
}

// StartWorker starts a workflow worker and activity worker based on configured options.
// The worker will listen for workflows registered with the same taskList
//
// param domainName string -> name of the cadence doamin for the worker to listen on
//
// param taskList string -> the name of the group of cadence workflows for the worker to listen for
//
// param options worker.Options -> Options used to configure a worker instance
//
// param workerID int64 -> the id of the new worker that will be mapped internally in
// the cadence-proxy
//
// returns worker.Worker -> the worker.Worker returned by the worker.New()
// call to the cadence server
//
// returns error -> an error if the workflow could not be started, or nil if
// the workflow was triggered successfully
func (helper *CadenceClientHelper) StartWorker(domain, taskList string, options worker.Options) (worker.Worker, error) {

	// set the worker logger
	// create and start the worker
	options.Logger = zap.L()
	worker := worker.New(helper.Service, domain, taskList, options)
	err := worker.Start()
	if err != nil {
		helper.Logger.Error("failed to start workers.", zap.Error(err))

		return nil, err
	}

	// $debug(jack.burns): DELETE THIS!
	helper.Logger.Debug("New Worker Created",
		zap.String("Domain", domain),
		zap.String("TaskList", taskList),
		zap.Int("ProccessId", os.Getpid()),
	)

	return worker, nil
}

// StopWorker stops a worker at the given workerID
//
// param worker.Worker -> the worker to be stopped
func (helper *CadenceClientHelper) StopWorker(worker worker.Worker) {
	worker.Stop()
}

// DescribeDomain creates a cadence domain client instance and calls .Describe()
// on it to get the description of a registered cadence domain
//
// param ctx context.Context -> the context to use to execute the describe domain
// request to cadence
//
// param domain string -> the domain you want to query
//
// returns *cadenceshared.DescribeDomainResponse -> response to the describe domain
// request
//
// returns error -> error if one is thrown, nil if the method executed with no errors
func (helper *CadenceClientHelper) DescribeDomain(ctx context.Context, domain string) (*cadenceshared.DescribeDomainResponse, error) {

	// build domain client
	domainClient, err := helper.Builder.BuildCadenceDomainClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build domain cadence client.", zap.Error(err))

		return nil, err
	}

	// domain describe call to cadence
	response, err := domainClient.Describe(ctx, domain)
	if err != nil {
		return nil, err
	}

	return response, nil
}

// RegisterDomain creates a cadence domain client instance and calls .Register()
// on it to register a cadence domain
//
// param ctx context.Context -> the context to use to execute the Register domain
// request to cadence
//
// param registerDomainRequest *cadenceshared.RegisterDomainRequest -> the request
// used to register the cadence domain
//
// returns error -> error if one is thrown, nil if the method executed with no errors
func (helper *CadenceClientHelper) RegisterDomain(ctx context.Context, registerDomainRequest *cadenceshared.RegisterDomainRequest) error {

	// build domain client
	domain := registerDomainRequest.GetName()
	domainClient, err := helper.Builder.BuildCadenceDomainClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build domain cadence client.", zap.Error(err))

		return err
	}

	// domain Register call to cadence
	err = domainClient.Register(ctx, registerDomainRequest)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		helper.Logger.Error("failed to register domain",
			zap.String("Domain Name", domain),
			zap.Error(err),
		)

		return err
	}

	// $debug(jack.burns): DELETE THIS!
	helper.Logger.Debug("domain successfully registered", zap.String("Domain Name", domain))

	return nil
}

// UpdateDomain creates a cadence domain client instance and calls .Update()
// on it to Update a cadence domain
//
// param ctx context.Context -> the context to use to execute the Update domain
// request to cadence
//
// param UpdateDomainRequest *cadenceshared.UpdateDomainRequest -> the request
// used to Update the cadence domain
//
// returns error -> error if one is thrown, nil if the method executed with no errors
func (helper *CadenceClientHelper) UpdateDomain(ctx context.Context, updateDomainRequest *cadenceshared.UpdateDomainRequest) error {

	// build domain client
	domain := updateDomainRequest.GetName()
	domainClient, err := helper.Builder.BuildCadenceDomainClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build domain cadence client.", zap.Error(err))

		return err
	}

	// domain Update call to cadence
	err = domainClient.Update(ctx, updateDomainRequest)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		helper.Logger.Error("failed to update domain",
			zap.String("Domain Name", domain),
			zap.Error(err),
		)

		return err
	}

	// $debug(jack.burns): DELETE THIS!
	helper.Logger.Debug("domain successfully updated", zap.String("Domain Name", domain))

	return nil
}

// ExecuteWorkflow is an instance method to execute a registered cadence workflow
//
// param ctx context.Context -> the context to use to execute the workflow
//
// param domain string -> the domain to start the workflow on
//
// param options client.StartWorkflowOptions -> configuration parameters for starting a workflow execution
//
// param workflow interface{} -> a registered cadence workflow
//
// param args ...interface{} -> anonymous number of arguments for starting a workflow
//
// returns client.WorkflowRun -> the client.WorkflowRun returned by the workflow execution
// call to the cadence server
//
// returns error -> an error if the workflow could not be started, or nil if
// the workflow was triggered successfully
func (helper *CadenceClientHelper) ExecuteWorkflow(ctx context.Context, domain string, options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) (client.WorkflowRun, error) {

	// set the domain
	// build the actual cadence client.Client
	helper.Builder = helper.Builder.SetDomain(domain)
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return nil, err
	}

	// start the workflow
	workflowRun, err := workflowClient.ExecuteWorkflow(ctx, options, workflow, args...)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to create workflow", zap.Error(err))
		return nil, err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Started Workflow",
		zap.String("WorkflowID", workflowRun.GetID()),
		zap.String("RunID", workflowRun.GetRunID()),
	)

	return workflowRun, nil
}

// GetWorkflow is an instance method to get a WorkflowRun from a started
// cadence workflow
//
// param ctx context.Context -> the context to use to get the workflow
//
// param workflowID string -> the workflowID of the running workflow
//
// param runID string -> the runID of the running workflow
//
// returns client.WorkflowRun -> the client.WorkflowRun returned by the GetWorkflow
// call to the cadence server
//
// returns error -> an error if the workflow could not be started, or nil if
// the workflow was triggered successfully
func (helper *CadenceClientHelper) GetWorkflow(ctx context.Context, workflowID, runID string) (client.WorkflowRun, error) {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return nil, err
	}

	// get the workflow execution
	workflowRun := workflowClient.GetWorkflow(ctx, workflowID, runID)

	// $debug(jack.burns)
	helper.Logger.Debug("GetWorkflow Successful",
		zap.String("WorkflowID", workflowRun.GetID()),
		zap.String("RunID", workflowRun.GetRunID()),
	)

	return workflowRun, nil
}

// CancelWorkflow is an instance method to cancel running
// cadence workflow
//
// param ctx context.Context -> the context to use to cancel the workflow
//
// param workflowID string -> the workflowID of the running workflow
//
// param runID string -> the runID of the running workflow
//
// returns error -> an error if the workflow could not be started, or nil if
// the workflow was cancelled successfully
func (helper *CadenceClientHelper) CancelWorkflow(ctx context.Context, workflowID, runID string) error {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return err
	}

	// cancel the workflow
	err = workflowClient.CancelWorkflow(ctx, workflowID, runID)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to cancel workflow",
			zap.String("WorkflowID", workflowID),
			zap.String("RunID", runID),
			zap.Error(err),
		)

		return err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Workflow Cancelled",
		zap.String("WorkflowID", workflowID),
		zap.String("RunID", runID),
	)

	return nil
}

// TerminateWorkflow is an instance method to terminate a running
// cadence workflow
//
// param ctx context.Context -> the context to use to terminate the workflow
//
// param workflowID string -> the workflowID of the running workflow
//
// param runID string -> the runID of the running workflow
//
// param reason string -> the string reason for terminating
//
// param details []byte -> termination details encoded as a []byte
//
// returns error -> an error if the workflow could not be started, or nil if
// the workflow was terminated successfully
func (helper *CadenceClientHelper) TerminateWorkflow(ctx context.Context, workflowID, runID, reason string, details []byte) error {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return err
	}

	// terminate the workflow
	err = workflowClient.TerminateWorkflow(ctx,
		workflowID,
		runID,
		reason,
		details,
	)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to terminate workflow",
			zap.String("WorkflowID", workflowID),
			zap.String("RunID", runID),
			zap.Error(err),
		)

		return err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Workflow Terminated",
		zap.String("WorkflowID", workflowID),
		zap.String("RunID", runID),
	)

	return nil
}

// SignalWithStartWorkflow is an instance method to signal a cadence workflow to start
//
// param ctx context.Context -> the context to use to get the workflow
//
// param workflowID string -> the workflowID of the running workflow
//
// param signalName string -> name of the signal to signal channel to signal the workflow
//
// param signalArg []byte -> the signalling arguments encoded as a []byte
//
// param signalOpts client.StartWorkflowOptions -> client.StartWorkflowOptions
// used to start the workflow
//
// param workflow string -> the name of the workflow to start
//
// param args ...interface{} -> the optional arguments for starting the workflow
//
// returns *workflow.Execution -> pointer to the resulting workflow execution from
// starting the workflow
//
// returns error -> error upon failure and nil upon success
func (helper *CadenceClientHelper) SignalWithStartWorkflow(ctx context.Context, workflowID, signalName string, signalArg []byte, opts client.StartWorkflowOptions, workflow string, args ...interface{}) (*workflow.Execution, error) {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return nil, err
	}

	// signal the workflow to start
	workflowExecution, err := workflowClient.SignalWithStartWorkflow(ctx, workflowID, signalName, signalArg, opts, workflow, args...)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to start workflow", zap.Error(err))
		return nil, err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Started Workflow",
		zap.String("WorkflowID", workflowExecution.ID),
		zap.String("RunID", workflowExecution.RunID),
	)

	return workflowExecution, nil
}

// DescribeWorkflowExecution is an instance method to describe the execution
// of a running cadence workflow
//
// param ctx context.Context -> the context to use to cancel the workflow
//
// param workflowID string -> the workflowID of the running workflow
//
// param runID string -> the runID of the running workflow
//
// returns *cadenceshared.DescribeWorkflowExecutionResponse -> the response to the
// describe workflow execution request
//
// returns error -> an error if the workflow could not be started, or nil if
// the workflow was cancelled successfully
func (helper *CadenceClientHelper) DescribeWorkflowExecution(ctx context.Context, workflowID, runID string) (*cadenceshared.DescribeWorkflowExecutionResponse, error) {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return nil, err
	}

	// descibe the workflow execution
	response, err := workflowClient.DescribeWorkflowExecution(ctx, workflowID, runID)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to describe workflow execution",
			zap.String("WorkflowID", workflowID),
			zap.String("RunID", runID),
			zap.Error(err),
		)

		return nil, err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Workflow Describe Execution Successful",
		zap.String("WorkflowID", workflowID),
		zap.String("RunID", runID),
	)

	return response, nil
}

// SignalWorkflow is an instance method to signal a cadence workflow
//
// param ctx context.Context -> the context to use to get the workflow
//
// param workflowID string -> the workflowID of the running workflow
//
// param runID string -> the runID of the running cadence workflow
//
// param signalName string -> name of the signal to signal channel to signal the workflow
//
// param arg interface{} -> the signaling arguments
//
// returns error -> error upon failure and nil upon success
func (helper *CadenceClientHelper) SignalWorkflow(ctx context.Context, workflowID, runID, signalName string, arg interface{}) error {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return err
	}

	// signal the workflow
	err = workflowClient.SignalWorkflow(ctx, workflowID, runID, signalName, arg)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to signal workflow", zap.Error(err))
		return err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Successfully Signaled Workflow",
		zap.String("WorkflowID", workflowID),
		zap.String("RunID", runID),
	)

	return nil
}

// QueryWorkflow is an instance method to query a cadence workflow
//
// param ctx context.Context -> the context to use to get the workflow
//
// param workflowID string -> the workflowID of the running workflow
//
// param runID string -> the runID of the running cadence workflow
//
// param queryType string -> name of the query to query channel to query the workflow
//
// param args ...interface{} -> the optional querying arguments
//
// returns encoded.Value -> the encoded result value of querying a workflow
//
// returns error -> error upon failure and nil upon success
func (helper *CadenceClientHelper) QueryWorkflow(ctx context.Context, workflowID, runID, queryType string, args ...interface{}) (encoded.Value, error) {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return nil, err
	}

	// query the workflow
	value, err := workflowClient.QueryWorkflow(ctx, workflowID, runID, queryType, args...)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to query workflow", zap.Error(err))
		return nil, err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Successfully Queried Workflow",
		zap.String("WorkflowID", workflowID),
		zap.String("RunID", runID),
	)

	return value, nil
}

// CompleteActivity is an instance method that completes the execution of an activity
//
// param ctx context.Context -> the go context used to execute the complete activity call
//
// param taskToken []byte -> a task token used to complete the activity encoded as
// a []byte
//
// param result interface{} -> the result to complete the activity with
//
// pararm err error -> error to complete the activity with
//
// returns error -> error upon failure to complete the activity, nil upon success
func (helper *CadenceClientHelper) CompleteActivity(ctx context.Context, taskToken []byte, result interface{}, e error) error {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to build cadence client.", zap.Error(err))
		return err
	}

	// query the workflow
	err = workflowClient.CompleteActivity(ctx, taskToken, result, e)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Error("failed to complete activity", zap.Error(err))
		return err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Successfully Completed Activity",
		zap.Any("Result", result),
		zap.Any("Error", e),
	)

	return nil
}
