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

{{- define "grafana.nodeSelector" -}}
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