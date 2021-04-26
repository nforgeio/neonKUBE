# Unit Test Automation

We're automating unit tests using our [nforgeio-actions/test](https://github.com/nforgeio-actions/test) GitHub Action 
which is then included various GitHub Action workflows.

We're also using the [LiquidTestReports.Markdown](https://dev.to/kurtmkurtm/testing-net-core-apps-with-github-actions-3i76)
test logger to generate nice markdown formatted reports which will be written to the runner's working directory like we do
for build logs.

## Configuring a new test project

Enabling this for a new test project is easy (just one step):

1. Add the **LiquidTestReports.Markdown** nuget package to the test project.
