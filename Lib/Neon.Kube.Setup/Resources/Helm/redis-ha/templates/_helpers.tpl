{{/* vim: set filetype=mustache: */}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
*/}}
{{- define "redis-ha.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
*/}}
{{- define "redis-ha.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}


{{/*
Return sysctl image
*/}}
{{- define "redis.sysctl.image" -}}
{{- $registryName :=  default "docker.io" .Values.sysctlImage.registry -}}
{{- $tag := default "latest" .Values.sysctlImage.tag | toString -}}
{{- printf "%s/%s:%s" $registryName .Values.sysctlImage.repository $tag -}}
{{- end -}}

{{- /*
Credit: @technosophos
https://github.com/technosophos/common-chart/
labels.standard prints the standard Helm labels.
The standard labels are frequently used in metadata.
*/ -}}
{{- define "labels.standard" -}}
app: {{ template "redis-ha.name" . }}
heritage: {{ .Release.Service | quote }}
release: {{ .Release.Name | quote }}
chart: {{ template "chartref" . }}
{{- end -}}

{{- /*
Credit: @technosophos
https://github.com/technosophos/common-chart/
chartref prints a chart name and version.
It does minimal escaping for use in Kubernetes labels.
Example output:
  zookeeper-1.2.3
  wordpress-3.2.1_20170219
*/ -}}
{{- define "chartref" -}}
  {{- replace "+" "_" .Chart.Version | printf "%s-%s" .Chart.Name -}}
{{- end -}}

{{/*
Create the name of the service account to use
*/}}
{{- define "redis-ha.serviceAccountName" -}}
{{- if .Values.serviceAccount.create -}}
    {{ default (include "redis-ha.fullname" .) .Values.serviceAccount.name }}
{{- else -}}
    {{ default "default" .Values.serviceAccount.name }}
{{- end -}}
{{- end -}}

{{- define "redis-ha.masterGroupName" -}}
{{- $masterGroupName := tpl ( .Values.redis.masterGroupName | default "") . -}}
{{- $validMasterGroupName := regexMatch "^[\\w-\\.]+$" $masterGroupName -}}
{{- if $validMasterGroupName -}}
{{ $masterGroupName }}
{{- else -}}
{{ required "A valid .Values.redis.masterGroupName entry is required (matching ^[\\w-\\.]+$)" ""}}
{{- end -}}
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

{{- define "redis-ha.nodeSelector" -}}
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