{{/*
Expand the name of the chart.
*/}}
{{- define "neon-cluster-operator.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "neon-cluster-operator.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "neon-cluster-operator.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "neon-cluster-operator.labels" -}}
helm.sh/chart: {{ include "neon-cluster-operator.chart" . }}
{{ include "neon-cluster-operator.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "neon-cluster-operator.selectorLabels" -}}
app.kubernetes.io/name: {{ include "neon-cluster-operator.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "neon-cluster-operator.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "neon-cluster-operator.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

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

{{- define "neon-cluster-operator.nodeSelector" -}}
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