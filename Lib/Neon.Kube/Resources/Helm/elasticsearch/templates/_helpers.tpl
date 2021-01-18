{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}
{{- define "name" -}}
{{- default .Release.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
*/}}
{{- define "fullname" -}}
{{- $name := default .Release.Name .Values.nameOverride -}}
{{- printf "%s-%s" $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "uname" -}}
{{ .Release.Name }}-{{ .Values.nodeGroup }}
{{- end -}}

{{- define "masterService" -}}
{{- if empty .Values.masterService -}}
{{ .Release.Name }}-master
{{- else -}}
{{ .Values.masterService }}
{{- end -}}
{{- end -}}

{{- define "endpoints" -}}
{{- $replicas := .Values.replicas | int }}
{{- $uname := printf "%s-%s" $.Release.Name .Values.nodeGroup }}
  {{- range $i, $e := untilStep 0 $replicas 1 -}}
{{ $uname }}-{{ $i }},
  {{- end -}}
{{- end -}}
