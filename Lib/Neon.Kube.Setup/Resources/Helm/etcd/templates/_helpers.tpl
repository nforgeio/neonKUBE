{{/*
Expand the name of the chart.
*/}}
{{- define "etcd.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "etcd.fullname" -}}
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
{{- define "etcd.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "etcd.labels" -}}
helm.sh/chart: {{ include "etcd.chart" . }}
{{ include "etcd.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "etcd.selectorLabels" -}}
app.kubernetes.io/name: {{ include "etcd.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Server selector labels
*/}}
{{- define "etcd.server.selectorLabels" -}}
app.kubernetes.io/component: server
{{ include "etcd.selectorLabels" . }}
{{- end }}

{{/*
Snapshotter selector labels
*/}}
{{- define "etcd.snapshotter.selectorLabels" -}}
app.kubernetes.io/component: snapshotter
{{ include "etcd.selectorLabels" . }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "etcd.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "etcd.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Return the proper etcd peer protocol
*/}}
{{- define "etcd.peerProtocol" -}}
{{- if .Values.auth.peer.secureTransport -}}
{{- print "https" -}}
{{- else -}}
{{- print "http" -}}
{{- end -}}
{{- end -}}

{{/*
Return the proper etcd client protocol
*/}}
{{- define "etcd.clientProtocol" -}}
{{- if .Values.auth.client.secureTransport -}}
{{- print "https" -}}
{{- else -}}
{{- print "http" -}}
{{- end -}}
{{- end -}}

{{/*
Return the proper etcdctl authentication options
*/}}
{{- define "etcd.authOptions" -}}
{{- $certsOption := " --cert=\"$ETCD_CERT_FILE\" --key=\"$ETCD_KEY_FILE\"" -}}
{{- $caOption := " --cacert=\"$ETCD_TRUSTED_CA_FILE\"" -}}
{{- if .Values.auth.client.secureTransport -}}
{{- printf "%s" $certsOption -}}
{{- end -}}
{{- if .Values.auth.client.enableAuthentication -}}
{{- printf "%s" $caOption -}}
{{- end -}}
{{- end -}}
