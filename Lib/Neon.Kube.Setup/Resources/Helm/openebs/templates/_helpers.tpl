{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}
{{- define "openebs.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "openebs.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "openebs.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create the name of the service account to use
*/}}
{{- define "openebs.serviceAccountName" -}}
{{- if .Values.serviceAccount.create -}}
    {{ default (include "openebs.fullname" .) .Values.serviceAccount.name }}
{{- else -}}
    {{ default "default" .Values.serviceAccount.name }}
{{- end -}}
{{- end -}}

{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}
{{- define "openebs-monitoring.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 50 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "openebs-monitoring.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 26 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 26 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 26 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Allow the release namespace to be overridden for multi-namespace deployments in combined charts
*/}}
{{- define "openebs-monitoring.namespace" -}}
  {{- if .Values.namespaceOverride -}}
    {{- .Values.namespaceOverride -}}
  {{- else -}}
    {{- .Release.Namespace -}}
  {{- end -}}
{{- end -}}


{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "openebs-monitoring.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "openebs-monitoring.labels" -}}
helm.sh/chart: {{ include "openebs-monitoring.chart" . }}
{{ include "openebs-monitoring.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "openebs-monitoring.selectorLabels" -}}
app.kubernetes.io/name: {{ include "openebs-monitoring.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
release: {{ $.Release.Name | quote }}
{{- end }}

{{/*
Template to support multiple levels of sub-charts

Call a template from the context of a subchart.

Usage:
  {{ include "call-nested" (list . "<subchart_name>" "<subchart_template_name>") }}
*/}}
{{- define "call-nested" }}
{{- $dot := index . 0 }}
{{- $subchart := index . 1 | splitList "." }}
{{- $template := index . 2 }}
{{- $values := $dot.Values }}
{{- range $subchart }}
{{- $values = index $values . }}
{{- end }}
{{- include $template (dict "Chart" (dict "Name" (last $subchart)) "Values" $values "Release" $dot.Release "Capabilities" $dot.Capabilities) }}
{{- end }}


{{/*
Define meta labels for openebs components
*/}}
{{- define "openebs.common.metaLabels" -}}
chart: {{ template "openebs.chart" . }}
heritage: {{ .Release.Service }}
openebs.io/version: {{ .Values.release.version | quote }}
{{- end -}}

{{- define "openebs.ndm-cluster-exporter.name" -}}
{{- $ndmName := default .Chart.Name .Values.ndmExporter.clusterExporter.nameOverride | trunc 63 | trimSuffix "-" }}
{{- $componentName := .Values.ndmExporter.clusterExporter.name | trunc 63 | trimSuffix "-" }}
{{- printf "%s-%s" $ndmName $componentName | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified ndm cluster exporter name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "openebs.ndm-cluster-exporter.fullname" -}}
{{- if .Values.ndmExporter.clusterExporter.fullnameOverride }}
{{- .Values.ndmExporter.clusterExporter.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $ndmClusterExporterName := include "openebs.ndm-cluster-exporter.name" .}}

{{- $name := default $ndmClusterExporterName .Values.ndmExporter.clusterExporter.nameOverride }}
{{- if contains .Release.Name $name }}
{{- $name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{- define "openebs.ndm-node-exporter.name" -}}
{{- $ndmName := default .Chart.Name .Values.ndmExporter.nodeExporter.nameOverride | trunc 63 | trimSuffix "-" }}
{{- $componentName := .Values.ndmExporter.nodeExporter.name | trunc 63 | trimSuffix "-" }}
{{- printf "%s-%s" $ndmName $componentName | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified ndm node exporter name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "openebs.ndm-node-exporter.fullname" -}}
{{- if .Values.ndmExporter.nodeExporter.fullnameOverride }}
{{- .Values.ndmExporter.nodeExporter.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $ndmNodeExporterName := include "openebs.ndm-node-exporter.name" .}}

{{- $name := default $ndmNodeExporterName .Values.ndmExporter.nodeExporter.nameOverride }}
{{- if contains .Release.Name $name }}
{{- $name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create match labels for ndm cluster exporter deployment
*/}}
{{- define "openebs.ndm-cluster-exporter.matchLabels" -}}
app: {{ template "openebs.ndm-cluster-exporter.name" . }}
release: {{ .Release.Name }}
component: {{ default (include "openebs.ndm-cluster-exporter.name" .) .Values.ndmExporter.clusterExporter.componentName }}
{{- end -}}

{{/*
Create component labels for ndm cluster exporter component
*/}}
{{- define "openebs.ndm-cluster-exporter.componentLabels" -}}
name: {{ template "openebs.ndm-node-exporter.name" . }}
openebs.io/component-name: {{ default (include "openebs.ndm-cluster-exporter.name" .) .Values.ndmExporter.clusterExporter.componentName }}
{{- end -}}


{{/*
Create labels for ndm cluster exporter component
*/}}
{{- define "openebs.ndm-cluster-exporter.labels" -}}
{{ include "openebs.common.metaLabels" . }}
{{ include "openebs.ndm-cluster-exporter.matchLabels" . }}
{{ include "openebs.ndm-cluster-exporter.componentLabels" . }}
{{- end -}}

{{/*
Create match labels for ndm node exporter deployment
*/}}
{{- define "openebs.ndm-node-exporter.matchLabels" -}}
app: {{ template "openebs.ndm-node-exporter.name" . }}
release: {{ .Release.Name }}
component: {{ default (include "openebs.ndm-node-exporter.name" .) .Values.ndmExporter.nodeExporter.componentName }}
{{- end -}}

{{/*
Create component labels for ndm node exporter component
*/}}
{{- define "openebs.ndm-node-exporter.componentLabels" -}}
name: {{ template "openebs.ndm-node-exporter.name" . }}
openebs.io/component-name: {{ default (include "openebs.ndm-node-exporter.name" .) .Values.ndmExporter.nodeExporter.componentName }}
{{- end -}}


{{/*
Create labels for ndm cluster node component
*/}}
{{- define "openebs.ndm-node-exporter.labels" -}}
{{ include "openebs.common.metaLabels" . }}
{{ include "openebs.ndm-node-exporter.matchLabels" . }}
{{ include "openebs.ndm-node-exporter.componentLabels" . }}
{{- end -}}

{{/*
Define node selectors.
*/}}
{{- define "nodeSelectorEnabled" -}}
{{- if .Values.nodeSelector -}}
{{- printf "true" }}
{{- else if .Values.nodeSelectors -}}
{{- printf "true" }}
{{- else -}}
{{- printf "false" }}
{{- end -}}
{{- end -}}

{{- define "openebs.nodeSelector" -}}
{{- if eq (include "nodeSelectorEnabled" .) "true" -}}
{{- if .Values.nodeSelector -}}
{{- range $key, $value := .Values.nodeSelector }}
{{- printf "%s: \"%s\"" $key $value }}
{{- end }}
{{- end }}
{{- if .Values.nodeSelectors -}}
{{- range $key := .Values.nodeSelectors }}
{{- printf "%s: \"%s\"" $key.key $key.value }}
{{- end -}}
{{- end -}}
{{- else -}}
{{- printf "{}" }}
{{- end -}}
{{- end -}}


