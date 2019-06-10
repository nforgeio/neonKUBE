//-----------------------------------------------------------------------------
// FILE:		helper.go
// CONTRIBUTOR: John C Burnes
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

	"go.uber.org/cadence/.gen/go/cadence/workflowserviceclient"
	"go.uber.org/cadence/worker"
	"go.uber.org/zap"

	"go.uber.org/cadence/client"
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

// StartWorkflow is an instance method that is starts a registered cadence workflow
//
// param options client.StartWorkflowOptions -> configuration parameters for starting a workflow execution
// param workflow interface{} -> a registered cadence workflow
// param args ...interface{} -> anonymous number of arguments for starting a workflow
//
// returns error -> an error if the workflow could not be started, or nil if
// the workflow was triggered successfully
func (helper *CadenceClientHelper) StartWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) error {

	// build the actual cadence client.Client
	workflowClient, err := helper.Builder.BuildCadenceClient()
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Debug("Failed to build cadence client.", zap.Error(err))
		return err
	}

	// start the workflow in the background context and store execution information
	workflowExecution, err := workflowClient.StartWorkflow(context.Background(), options, workflow, args...)
	if err != nil {

		// $debug(jack.burns)
		helper.Logger.Debug("Failed to create workflow", zap.Error(err))
		return err
	}

	// $debug(jack.burns)
	helper.Logger.Debug("Started Workflow",
		zap.String("WorkflowID", workflowExecution.ID),
		zap.String("RunID", workflowExecution.RunID))

	return nil
}

// StartWorkers starts a workflow worker and activity worker based on configured options.
// The worker will listen for workflows registered with the same groupName
//
// param domainName string -> name of the cadence doamin for the worker to listen on
// param groupName string -> the name of the group of cadence workflows for the worker to listen for
// options worker.Options -> Options used to configure a worker instance
func (helper *CadenceClientHelper) StartWorkers(domainName, groupName string, options worker.Options) {
	worker := worker.New(helper.Service, domainName, groupName, options)
	err := worker.Start()
	if err != nil {
		helper.Logger.Panic("Failed to start workers.", zap.Error(err))
	}
}
