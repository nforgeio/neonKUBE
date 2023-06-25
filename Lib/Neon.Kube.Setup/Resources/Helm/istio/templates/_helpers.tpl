{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "defaultNodeSelectorEnabled" -}}
{{- if .Values.defaultNodeSelector -}}
{{- printf "true" }}
{{- else if .Values.defaultNodeSelectors -}}
{{- printf "true" }}
{{- else -}}
{{- printf "false" }}
{{- end -}}
{{- end -}}

{{- define "istio.defaultNodeSelector" -}}
{{- if eq (include "defaultNodeSelectorEnabled" .) "true" -}}
{{- if .Values.defaultNodeSelector -}}
{{- range $key, $value := .Values.defaultNodeSelector }}
{{- printf "%s: %s" $key $value }}
{{- end }}
{{- end }}
{{- if .Values.defaultNodeSelectors -}}
{{- range $key := .Values.defaultNodeSelectors }}
{{- printf "%s: %s" $key.key $key.value }}
{{- end -}}
{{- end -}}
{{- else -}}
{{- printf "{}" }}
{{- end -}}
{{- end -}}