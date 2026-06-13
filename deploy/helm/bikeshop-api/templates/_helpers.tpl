{{/*
Nom de base du chart (tronqué à 63 caractères pour respecter les limites DNS).
*/}}
{{- define "bikeshop-api.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Nom complet des ressources.
*/}}
{{- define "bikeshop-api.fullname" -}}
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
Nom du chart et version, pour le label helm.sh/chart.
*/}}
{{- define "bikeshop-api.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Labels communs.
*/}}
{{- define "bikeshop-api.labels" -}}
helm.sh/chart: {{ include "bikeshop-api.chart" . }}
{{ include "bikeshop-api.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Labels de sélection (immuables : ne pas y ajouter de version).
*/}}
{{- define "bikeshop-api.selectorLabels" -}}
app.kubernetes.io/name: {{ include "bikeshop-api.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Nom du ServiceAccount à utiliser.
*/}}
{{- define "bikeshop-api.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "bikeshop-api.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Nom du PVC (claim existant si fourni, sinon nom dérivé du release).
*/}}
{{- define "bikeshop-api.pvcName" -}}
{{- if .Values.persistence.existingClaim }}
{{- .Values.persistence.existingClaim }}
{{- else }}
{{- printf "%s-data" (include "bikeshop-api.fullname" .) }}
{{- end }}
{{- end }}

{{/*
Tag d'image : image.tag si défini, sinon appVersion.
*/}}
{{- define "bikeshop-api.imageTag" -}}
{{- default .Chart.AppVersion .Values.image.tag }}
{{- end }}
