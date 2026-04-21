"use client"

import { useState } from "react"
import {
  FileCheck2,
  Building2,
  ArrowLeftRight,
  AlertTriangle,
} from "lucide-react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { MetricCard } from "./metric-card"
import { ConsentCard } from "./consent-card"
import {
  PieChart,
  Pie,
  Cell,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts"
import { consents as initialConsents, riskData, dataFlowData } from "@/lib/mock-data"

export function DashboardContent() {
  const [consents, setConsents] = useState(initialConsents)

  const handleRevoke = (id: string) => {
    setConsents((prev) => prev.filter((c) => c.id !== id))
  }

  const activeConsents = consents.filter((c) => c.status === "active")

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Dashboard</h1>
        <p className="text-muted-foreground">
          Overview of your data permissions and consent status
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <MetricCard
          title="Active Consents"
          value={activeConsents.length}
          description="Total active permissions"
          icon={FileCheck2}
          variant="default"
        />
        <MetricCard
          title="Organizations"
          value={8}
          description="Services with data access"
          icon={Building2}
          variant="success"
        />
        <MetricCard
          title="Data Transfers"
          value={94}
          description="This month"
          icon={ArrowLeftRight}
          trend={{ value: 12, isPositive: false }}
          variant="warning"
        />
        <MetricCard
          title="High Risk Services"
          value={3}
          description="Require attention"
          icon={AlertTriangle}
          variant="danger"
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Risk Overview</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-center">
              <ResponsiveContainer width="100%" height={250}>
                <PieChart>
                  <Pie
                    data={riskData}
                    cx="50%"
                    cy="50%"
                    innerRadius={60}
                    outerRadius={100}
                    paddingAngle={5}
                    dataKey="value"
                    label={({ name, value }) => `${name}: ${value}`}
                    labelLine={false}
                  >
                    {riskData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.fill} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="mt-4 flex justify-center gap-6">
              {riskData.map((item) => (
                <div key={item.name} className="flex items-center gap-2">
                  <div
                    className="h-3 w-3 rounded-full"
                    style={{ backgroundColor: item.fill }}
                  />
                  <span className="text-sm text-muted-foreground">
                    {item.name}
                  </span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Data Transfer Trend</CardTitle>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={250}>
              <AreaChart data={dataFlowData}>
                <defs>
                  <linearGradient id="colorTransfers" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="var(--color-primary)" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="var(--color-primary)" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                <XAxis
                  dataKey="month"
                  className="text-xs"
                  tick={{ fill: "var(--color-muted-foreground)" }}
                />
                <YAxis
                  className="text-xs"
                  tick={{ fill: "var(--color-muted-foreground)" }}
                />
                <Tooltip
                  contentStyle={{
                    backgroundColor: "var(--color-card)",
                    border: "1px solid var(--color-border)",
                    borderRadius: "8px",
                  }}
                />
                <Area
                  type="monotone"
                  dataKey="transfers"
                  stroke="var(--color-primary)"
                  strokeWidth={2}
                  fillOpacity={1}
                  fill="url(#colorTransfers)"
                />
              </AreaChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Recent Consents</CardTitle>
          <span className="text-sm text-muted-foreground">
            {activeConsents.length} active
          </span>
        </CardHeader>
        <CardContent className="space-y-3">
          {activeConsents.map((consent) => (
            <ConsentCard
              key={consent.id}
              consent={consent}
              onRevoke={handleRevoke}
            />
          ))}
        </CardContent>
      </Card>
    </div>
  )
}
