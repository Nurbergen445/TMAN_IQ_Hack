"use client"

import { useState } from "react"
import { Filter, Grid, List } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { OrganizationCard } from "./organization-card"
import { organizations as initialOrganizations } from "@/lib/mock-data"
import type { RiskLevel } from "@/lib/types"
import { cn } from "@/lib/utils"

export function OrganizationsContent() {
  const [organizations, setOrganizations] = useState(initialOrganizations)
  const [searchQuery, setSearchQuery] = useState("")
  const [riskFilter, setRiskFilter] = useState<RiskLevel | "all">("all")
  const [viewMode, setViewMode] = useState<"grid" | "list">("grid")

  const handleRevoke = (id: string) => {
    setOrganizations((prev) => prev.filter((org) => org.id !== id))
  }

  const filteredOrganizations = organizations.filter((org) => {
    const matchesSearch = org.name
      .toLowerCase()
      .includes(searchQuery.toLowerCase())
    const matchesRisk = riskFilter === "all" || org.riskLevel === riskFilter
    return matchesSearch && matchesRisk
  })

  const riskCounts = {
    all: organizations.length,
    low: organizations.filter((o) => o.riskLevel === "low").length,
    medium: organizations.filter((o) => o.riskLevel === "medium").length,
    high: organizations.filter((o) => o.riskLevel === "high").length,
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Data Organizations</h1>
        <p className="text-muted-foreground">
          Manage access permissions for connected services
        </p>
      </div>

      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-1 gap-3">
          <Input
            placeholder="Search organizations..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="max-w-sm"
          />
          <Select
            value={riskFilter}
            onValueChange={(value) => setRiskFilter(value as RiskLevel | "all")}
          >
            <SelectTrigger className="w-48">
              <Filter className="mr-2 h-4 w-4" />
              <SelectValue placeholder="Filter by risk" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Levels ({riskCounts.all})</SelectItem>
              <SelectItem value="low">Low Risk ({riskCounts.low})</SelectItem>
              <SelectItem value="medium">
                Medium Risk ({riskCounts.medium})
              </SelectItem>
              <SelectItem value="high">High Risk ({riskCounts.high})</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-2 rounded-lg border border-border p-1">
          <Button
            variant={viewMode === "grid" ? "secondary" : "ghost"}
            size="icon"
            className="h-8 w-8"
            onClick={() => setViewMode("grid")}
          >
            <Grid className="h-4 w-4" />
            <span className="sr-only">Grid view</span>
          </Button>
          <Button
            variant={viewMode === "list" ? "secondary" : "ghost"}
            size="icon"
            className="h-8 w-8"
            onClick={() => setViewMode("list")}
          >
            <List className="h-4 w-4" />
            <span className="sr-only">List view</span>
          </Button>
        </div>
      </div>

      {filteredOrganizations.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-border py-16">
          <p className="text-lg font-medium">No organizations found</p>
          <p className="text-sm text-muted-foreground">
            Try adjusting your search or filters
          </p>
        </div>
      ) : (
        <div
          className={cn(
            viewMode === "grid"
              ? "grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4"
              : "space-y-4"
          )}
        >
          {filteredOrganizations.map((organization) => (
            <OrganizationCard
              key={organization.id}
              organization={organization}
              onRevoke={handleRevoke}
            />
          ))}
        </div>
      )}
    </div>
  )
}
