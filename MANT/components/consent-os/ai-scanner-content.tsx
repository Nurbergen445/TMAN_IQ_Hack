"use client"

import { useState } from "react"
import {
  ScanSearch,
  AlertTriangle,
  Clock,
  Shield,
  Lightbulb,
  ChevronRight,
  Loader2,
  FileText,
  Sparkles,
} from "lucide-react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Badge } from "@/components/ui/badge"
import { Progress } from "@/components/ui/progress"
import { cn } from "@/lib/utils"
import type { AIScanResult } from "@/lib/types"

const sampleTerms = `By using our services, you agree that we may collect, store, and process your personal information including but not limited to: name, email address, phone number, location data, browsing history, device identifiers, and usage patterns. 

We may share your information with third-party partners for marketing purposes, analytics, and service improvement. Your data may be transferred to servers located in countries outside your jurisdiction.

We retain your personal data for as long as necessary to provide our services, which may extend beyond the termination of your account. You grant us a worldwide, non-exclusive license to use, modify, and distribute content you create using our platform.

By continuing to use our services, you consent to receiving promotional communications via email, SMS, and push notifications. You may opt-out at any time, though some service notifications will remain mandatory.`

const mockScanResult: AIScanResult = {
  summary:
    "This agreement grants the service extensive data collection rights including personal information, location tracking, and behavioral data. The company can share your data with third parties and retain it indefinitely. You also grant them rights to your created content.",
  redFlags: [
    "Broad data sharing with unspecified third parties for marketing",
    "Data retention extends beyond account termination",
    "Worldwide license granted over user-created content",
    "Data transfers to jurisdictions with weaker privacy laws",
    "Mandatory service notifications cannot be disabled",
  ],
  dataRetentionPeriod: "Indefinite (extends beyond account termination)",
  riskScore: 75,
  recommendations: [
    "Consider limiting data sharing permissions in account settings",
    "Regularly review and delete unnecessary personal data",
    "Use a dedicated email for this service",
    "Opt-out of marketing communications immediately",
    "Consider alternative services with better privacy practices",
  ],
}

export function AIScannerContent() {
  const [termsText, setTermsText] = useState("")
  const [isScanning, setIsScanning] = useState(false)
  const [scanResult, setScanResult] = useState<AIScanResult | null>(null)

  const handleScan = async () => {
    if (!termsText.trim()) return

    setIsScanning(true)
    setScanResult(null)

    // Simulate AI processing delay
    await new Promise((resolve) => setTimeout(resolve, 2500))

    setScanResult(mockScanResult)
    setIsScanning(false)
  }

  const handleUseSample = () => {
    setTermsText(sampleTerms)
    setScanResult(null)
  }

  const getRiskColor = (score: number) => {
    if (score < 40) return "text-success"
    if (score < 70) return "text-warning"
    return "text-destructive"
  }

  const getRiskLabel = (score: number) => {
    if (score < 40) return "Low Risk"
    if (score < 70) return "Medium Risk"
    return "High Risk"
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">AI Legal Scanner</h1>
        <p className="text-muted-foreground">
          Analyze Terms of Service and Privacy Policies with AI-powered insights
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="lg:row-span-2">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FileText className="h-5 w-5" />
              Legal Text Input
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <Textarea
              placeholder="Paste Terms of Service, Privacy Policy, or any legal agreement here..."
              className="min-h-[300px] resize-none font-mono text-sm"
              value={termsText}
              onChange={(e) => {
                setTermsText(e.target.value)
                setScanResult(null)
              }}
            />

            <div className="flex items-center justify-between">
              <Button variant="outline" size="sm" onClick={handleUseSample}>
                Use Sample Text
              </Button>
              <span className="text-sm text-muted-foreground">
                {termsText.length} characters
              </span>
            </div>

            <Button
              className="w-full gap-2"
              size="lg"
              onClick={handleScan}
              disabled={!termsText.trim() || isScanning}
            >
              {isScanning ? (
                <>
                  <Loader2 className="h-5 w-5 animate-spin" />
                  Analyzing with AI...
                </>
              ) : (
                <>
                  <Sparkles className="h-5 w-5" />
                  Simplify with AI
                </>
              )}
            </Button>

            <p className="text-center text-xs text-muted-foreground">
              Powered by AI. Ready for Gemini 1.5 Pro integration.
            </p>
          </CardContent>
        </Card>

        {isScanning && (
          <Card className="lg:col-start-2">
            <CardContent className="flex flex-col items-center justify-center py-16">
              <div className="relative">
                <div className="absolute inset-0 flex items-center justify-center">
                  <div className="h-16 w-16 rounded-full border-4 border-primary/30" />
                </div>
                <ScanSearch className="h-16 w-16 animate-pulse text-primary" />
              </div>
              <p className="mt-6 text-lg font-medium">Analyzing Legal Text</p>
              <p className="mt-2 text-sm text-muted-foreground">
                Identifying key clauses and potential concerns...
              </p>
              <div className="mt-6 w-full max-w-xs">
                <Progress value={66} className="h-2" />
              </div>
            </CardContent>
          </Card>
        )}

        {scanResult && !isScanning && (
          <>
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Shield className="h-5 w-5" />
                  Risk Assessment
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p
                      className={cn(
                        "text-4xl font-bold",
                        getRiskColor(scanResult.riskScore)
                      )}
                    >
                      {scanResult.riskScore}/100
                    </p>
                    <p className="text-sm text-muted-foreground">Risk Score</p>
                  </div>
                  <Badge
                    variant="outline"
                    className={cn(
                      "text-sm px-3 py-1",
                      scanResult.riskScore >= 70
                        ? "bg-destructive/10 text-destructive border-destructive/20"
                        : scanResult.riskScore >= 40
                          ? "bg-warning/10 text-warning-foreground border-warning/20"
                          : "bg-success/10 text-success border-success/20"
                    )}
                  >
                    {getRiskLabel(scanResult.riskScore)}
                  </Badge>
                </div>

                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span>Privacy Risk</span>
                    <span className="font-medium">High</span>
                  </div>
                  <Progress value={85} className="h-2" />
                </div>

                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span>Data Sharing</span>
                    <span className="font-medium">Medium</span>
                  </div>
                  <Progress value={60} className="h-2" />
                </div>

                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span>User Rights</span>
                    <span className="font-medium">Low</span>
                  </div>
                  <Progress value={35} className="h-2" />
                </div>
              </CardContent>
            </Card>

            <Card className="lg:col-span-2">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <ScanSearch className="h-5 w-5" />
                  Human-Readable Summary
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-muted-foreground leading-relaxed">
                  {scanResult.summary}
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-destructive">
                  <AlertTriangle className="h-5 w-5" />
                  Potential Red Flags
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="space-y-3">
                  {scanResult.redFlags.map((flag, index) => (
                    <li key={index} className="flex items-start gap-3">
                      <div className="mt-0.5 h-2 w-2 rounded-full bg-destructive" />
                      <span className="text-sm">{flag}</span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Clock className="h-5 w-5" />
                  Data Retention Period
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="rounded-lg bg-warning/10 p-4 border border-warning/20">
                  <p className="font-medium text-warning-foreground">
                    {scanResult.dataRetentionPeriod}
                  </p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Your data may be kept even after you delete your account
                  </p>
                </div>
              </CardContent>
            </Card>

            <Card className="lg:col-span-2">
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-success">
                  <Lightbulb className="h-5 w-5" />
                  Recommendations
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="grid gap-3 md:grid-cols-2">
                  {scanResult.recommendations.map((rec, index) => (
                    <li
                      key={index}
                      className="flex items-start gap-3 rounded-lg bg-muted p-3"
                    >
                      <ChevronRight className="mt-0.5 h-4 w-4 text-success" />
                      <span className="text-sm">{rec}</span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          </>
        )}

        {!scanResult && !isScanning && (
          <Card className="lg:col-start-2 lg:row-span-2">
            <CardContent className="flex flex-col items-center justify-center h-full py-16">
              <div className="flex h-20 w-20 items-center justify-center rounded-full bg-muted">
                <ScanSearch className="h-10 w-10 text-muted-foreground" />
              </div>
              <p className="mt-6 text-lg font-medium">No Analysis Yet</p>
              <p className="mt-2 text-center text-sm text-muted-foreground max-w-xs">
                Paste a Terms of Service or Privacy Policy on the left and click
                &quot;Simplify with AI&quot; to get started.
              </p>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  )
}
