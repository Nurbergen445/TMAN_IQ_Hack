export type RiskLevel = "low" | "medium" | "high"

export type DataCategory = "personal" | "financial" | "educational" | "health" | "location" | "behavioral"

export interface Organization {
  id: string
  name: string
  logo: string
  accessLevel: string[]
  lastSync: string
  riskLevel: RiskLevel
  dataTypes: DataCategory[]
  description: string
}

export interface Consent {
  id: string
  organizationId: string
  organizationName: string
  dataType: string
  purpose: string
  grantedAt: string
  expiresAt?: string
  status: "active" | "revoked" | "expired"
}

export interface PermissionEvent {
  id: string
  organizationId: string
  organizationName: string
  organizationLogo: string
  dataType: string
  action: "granted" | "revoked" | "accessed" | "updated"
  purpose: string
  timestamp: string
}

export interface AIScanResult {
  summary: string
  redFlags: string[]
  dataRetentionPeriod: string
  riskScore: number
  recommendations: string[]
}
