"use client"

import { useState } from "react"
import { format } from "date-fns"
import { MoreVertical, Eye, Trash2 } from "lucide-react"
import { Card, CardContent, CardHeader } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { RevokeModal } from "./revoke-modal"
import type { Organization } from "@/lib/types"
import { cn } from "@/lib/utils"

interface OrganizationCardProps {
  organization: Organization
  onRevoke?: (id: string) => void
}

export function OrganizationCard({ organization, onRevoke }: OrganizationCardProps) {
  const [revokeModalOpen, setRevokeModalOpen] = useState(false)

  const riskColors = {
    low: "bg-success/10 text-success border-success/20",
    medium: "bg-warning/10 text-warning-foreground border-warning/20",
    high: "bg-destructive/10 text-destructive border-destructive/20",
  }

  const riskLabels = {
    low: "Low Risk",
    medium: "Medium Risk",
    high: "High Risk",
  }

  const logoBgColors = {
    low: "bg-success/20 text-success",
    medium: "bg-warning/20 text-warning-foreground",
    high: "bg-destructive/20 text-destructive",
  }

  return (
    <>
      <Card className="group transition-shadow hover:shadow-md">
        <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-2">
          <div className="flex items-center gap-3">
            <div
              className={cn(
                "flex h-12 w-12 items-center justify-center rounded-lg font-bold text-lg",
                logoBgColors[organization.riskLevel]
              )}
            >
              {organization.logo}
            </div>
            <div>
              <h3 className="font-semibold">{organization.name}</h3>
              <p className="text-sm text-muted-foreground">
                Last sync: {format(new Date(organization.lastSync), "MMM d, yyyy")}
              </p>
            </div>
          </div>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="h-8 w-8 opacity-0 transition-opacity group-hover:opacity-100">
                <MoreVertical className="h-4 w-4" />
                <span className="sr-only">More options</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem>
                <Eye className="mr-2 h-4 w-4" />
                View Details
              </DropdownMenuItem>
              <DropdownMenuItem
                className="text-destructive"
                onClick={() => setRevokeModalOpen(true)}
              >
                <Trash2 className="mr-2 h-4 w-4" />
                Revoke Access
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </CardHeader>

        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground line-clamp-2">
            {organization.description}
          </p>

          <div>
            <p className="mb-2 text-xs font-medium text-muted-foreground uppercase tracking-wider">
              Access Level
            </p>
            <div className="flex flex-wrap gap-1.5">
              {organization.accessLevel.map((access) => (
                <Badge key={access} variant="secondary" className="text-xs">
                  {access}
                </Badge>
              ))}
            </div>
          </div>

          <div className="flex items-center justify-between pt-2 border-t border-border">
            <Badge
              variant="outline"
              className={cn("text-xs", riskColors[organization.riskLevel])}
            >
              {riskLabels[organization.riskLevel]}
            </Badge>
            <Button
              variant="ghost"
              size="sm"
              className="text-destructive hover:text-destructive hover:bg-destructive/10"
              onClick={() => setRevokeModalOpen(true)}
            >
              Revoke
            </Button>
          </div>
        </CardContent>
      </Card>

      <RevokeModal
        open={revokeModalOpen}
        onOpenChange={setRevokeModalOpen}
        organizationName={organization.name}
        dataTypes={organization.accessLevel}
        onConfirm={() => onRevoke?.(organization.id)}
      />
    </>
  )
}
