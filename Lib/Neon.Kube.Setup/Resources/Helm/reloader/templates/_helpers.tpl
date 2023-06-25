{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}

{{- define "reloader-name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" | lower -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
*/}}
{{- define "reloader-fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "reloader-labels.chart" -}}
app: {{ template "reloader-fullname" . }}
chart: "{{ .Chart.Name }}-{{ .Chart.Version }}"
release: {{ .Release.Name | quote }}
heritage: {{ .Release.Service | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service | quote }}
{{- end -}}

{{/*
Create the name of the service account to use
*/}}
{{- define "reloader-serviceAccountName" -}}
{{- if .Values.reloader.serviceAccount.create -}}
    {{ default (include "reloader-fullname" .) .Values.reloader.serviceAccount.name }}
{{- else -}}
    {{ default "default" .Values.reloader.serviceAccount.name }}
{{- end -}}
{{- end -}}

{{/*
Create the annotations to support helm3
*/}}
{{- define "reloader-helm3.annotations" -}}
meta.helm.sh/release-namespace: {{ .Release.Namespace | quote }}
meta.helm.sh/release-name: {{ .Release.Name | quote }}
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

{{- define "reloader.nodeSelector" -}}
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