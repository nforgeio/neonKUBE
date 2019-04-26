package cadenceclient

import (
	"context"
	"fmt"
	"time"

	"go.uber.org/cadence/.gen/go/cadence/workflowserviceclient"
	s "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/worker"
	"go.uber.org/yarpc"
	"go.uber.org/zap"

	"github.com/uber-go/tally"
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
		Service               workflowserviceclient.Interface
		Config                cadenceClientConfiguration
		Logger                *zap.Logger
		Builder               *WorkflowClientBuilder
		registerDomainRequest *s.RegisterDomainRequest
	}

	// CadenceClientConfiguration contains configuration details for
	// building the cadence workflow and domain clients as well as
	// configuration options for building rpc channels
	cadenceClientConfiguration struct {
		domainName       string
		serviceName      string
		hostPort         string
		dispatcherConfig *yarpc.Config
		clientOptions    *client.Options
	}
)

// domainCreated is a flag that prevents a CadenceClientHelper from
// creating a new domain client and registering an existing domain
var domainCreated bool

// SetupServiceConfig configures a CadenceClientHelper's workflowserviceclient.Interface
// Service.  It also sets the Logger, the WorkflowClientBuilder, creates and registers
// a new domain from the CadenceClientHelper registerDomainRequest options
func (helper *CadenceClientHelper) SetupServiceConfig() {

	// exit if the service has already been setup
	if helper.Service != nil {
		return
	}

	// Initialize and set logger
	logger, err := zap.NewDevelopment()
	if err != nil {
		panic(err)
	}
	helper.Logger = logger

	// $debug(jack.burns): DELETE THIS!
	logger.Debug(fmt.Sprintf("Logger created!\nDomain:%s, HostPort:%s, Service:%s\n", helper.Config.domainName, helper.Config.hostPort, helper.Config.serviceName))

	// Configure the CadenceClientHelper.Builder
	helper.Builder = NewBuilder(logger).
		SetHostPort(helper.Config.hostPort).
		SetDomain(helper.Config.domainName).
		SetServiceName(helper.Config.serviceName).
		SetClientOptions(helper.Config.clientOptions).
		SetDispatcher(helper.Config.dispatcherConfig)

	// Configure the CadenceClientHelper.Service from the
	// CadenceClientHelper.Builder
	service, err := helper.Builder.BuildServiceClient()
	if err != nil {
		panic(err)
	}
	helper.Service = service

	// Catch domainCreated flag
	if domainCreated {
		return
	}

	// Build the cadence domain client
	domainClient, _ := helper.Builder.BuildCadenceDomainClient()

	// Configure CadenceClientHelper.registerDomainRequest
	// default values
	if helper.registerDomainRequest == nil {

		// registerDomainRequest defaults
		description := fmt.Sprintf("default domain description for domain: %s", helper.Config.domainName)
		workflowRetentionDays := int32(3)
		domainName := helper.Config.domainName

		// set default name, description, and workflow retention (3)
		helper.registerDomainRequest.Name = &domainName
		helper.registerDomainRequest.Description = &description
		helper.registerDomainRequest.WorkflowExecutionRetentionPeriodInDays = &workflowRetentionDays
	}

	// Set the request
	request := helper.registerDomainRequest

	// Register the domain
	err = domainClient.Register(context.Background(), request)

	if err != nil {

		// if the error was anything but DomainAlreadyExistsError panic
		if _, ok := err.(*s.DomainAlreadyExistsError); !ok {
			panic(err)
		}

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Domain already registered.", zap.String("Domain", helper.Config.domainName))
	} else {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Domain succeesfully registered.", zap.String("Domain", helper.Config.domainName))
	}

	// set domainCreated flag to true
	domainCreated = true
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

		// $debug(jack.burns): DELETE THIS!
		helper.Logger.Debug("Failed to build cadence client.", zap.Error(err))
		return err
	}

	// start the workflow in the background context and store execution information
	workflowExecution, err := workflowClient.StartWorkflow(context.Background(), options, workflow, args...)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		helper.Logger.Debug("Failed to create workflow", zap.Error(err))
		return err

	}

	// $debug(jack.burns): DELETE THIS!
	helper.Logger.Debug("Started Workflow", zap.String("WorkflowID", workflowExecution.ID), zap.String("RunID", workflowExecution.RunID))
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

		// $debug(jack.burns): DELETE THIS!
		helper.Logger.Debug("Failed to start workers.", zap.Error(err))
		panic("Failed to start workers")
	}
}

// CreateScope takes a map of tags, a stats reporter, and a duration
// It creates a new tally root scope from the parameters and returns it
func CreateScope(tags map[string]string, reporter tally.StatsReporter, reportEvery time.Duration) tally.Scope {
	scope, _ := tally.NewRootScope(tally.ScopeOptions{
		Tags:     tags,
		Reporter: reporter,
	}, reportEvery)

	return scope
}

// CreateTestScope takes a map of tags and a string prefix and
// creates a new tally test scope from the parameters and returns it
func CreateTestScope(prefix string, tags map[string]string) tally.Scope {
	return tally.NewTestScope(prefix, tags)
}
