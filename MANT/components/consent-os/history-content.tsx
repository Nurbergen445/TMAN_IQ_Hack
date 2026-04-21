"use client"

import { useState } from "react"
import { format, formatDistanceToNow } from "date-fns"
import {
  Check,
  X,
  RefreshCw,
  Eye,
  Filter,
  Download,
  Calendar,
} from "lucide-react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { permissionHistory } from "@/lib/mock-data"
import type { PermissionEvent } from "@/lib/types"
import { cn } from "@/lib/utils"

const actionIcons = {
  granted: Check,
  revoked: X,
  accessed: Eye,
  updated: RefreshCw,
}

const actionColors = {
  granted: "text-success bg-success/10",
  revoked: "text-destructive bg-destructive/10",
  accessed: "text-primary bg-primary/10",
  updated: "text-warning bg-warning/10",
}

const actionLabels = {
  granted: "Granted",
  revoked: "Revoked",
  accessed: "Accessed",
  updated: "Updated",
}

function TimelineItem({ event }: { event: PermissionEvent }) {
  const Icon = actionIcons[event.action]

  return (
    <div className="flex gap-4">
      <div className="flex flex-col items-center">
        <div
          className={cn(
            "flex h-10 w-10 items-center justify-center rounded-full",
            actionColors[event.action]
          )}
        >
          <Icon className="h-5 w-5" />
        </div>
        <div className="flex-1 w-px bg-border" />
      </div>

      <div className="flex-1 pb-8">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-3">
            <div
              className={cn(
                "flex h-8 w-8 items-center justify-center rounded-lg font-semibold text-sm",
                event.action === "revoked"
                  ? "bg-destructive/20 text-destructive"
                  : "bg-primary/20 text-primary"
              )}
            >
              {event.organizationLogo}
            </div>
            <div>
              <p className="font-medium">{event.organizationName}</p>
              <p className="text-sm text-muted-foreground">{event.purpose}</p>
            </div>
          </div>
          <div className="text-right">
            <Badge
              variant="outline"
              className={cn("text-xs", actionColors[event.action])}
            >
              {actionLabels[event.action]}
            </Badge>
            <p className="mt-1 text-xs text-muted-foreground">
              {formatDistanceToNow(new Date(event.timestamp), {
                addSuffix: true,
              })}
            </p>
          </div>
        </div>
        <div className="mt-3 rounded-lg bg-muted p-3">
          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">Data Type</span>
            <span className="font-medium">{event.dataType}</span>
          </div>
        </div>
      </div>
    </div>
  )
}

export function HistoryContent() {
  const [actionFilter, setActionFilter] = useState<string>("all")
  const [viewMode, setViewMode] = useState<"timeline" | "table">("timeline")

  const filteredHistory =
    actionFilter === "all"
      ? permissionHistory
      : permissionHistory.filter((event) => event.action === actionFilter)

  const actionCounts = {
    all: permissionHistory.length,
    granted: permissionHistory.filter((e) => e.action === "granted").length,
    revoked: permissionHistory.filter((e) => e.action === "revoked").length,
    accessed: permissionHistory.filter((e) => e.action === "accessed").length,
    updated: permissionHistory.filter((e) => e.action === "updated").length,
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold">Permission History</h1>
          <p className="text-muted-foreground">
            Complete timeline of your data access events
          </p>
        </div>
        <Button variant="outline" className="gap-2">
          <Download className="h-4 w-4" />
          Export Log
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        {(["granted", "revoked", "accessed", "updated"] as const).map(
          (action) => {
            const Icon = actionIcons[action]
            return (
              <Card
                key={action}
                className={cn(
                  "cursor-pointer transition-colors",
                  actionFilter === action && "ring-2 ring-primary"
                )}
                onClick={() =>
                  setActionFilter(actionFilter === action ? "all" : action)
                }
              >
                <CardContent className="flex items-center gap-4 p-4">
                  <div
                    className={cn(
                      "flex h-10 w-10 items-center justify-center rounded-lg",
                      actionColors[action]
                    )}
                  >
                    <Icon className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="text-2xl font-bold">{actionCounts[action]}</p>
                    <p className="text-sm text-muted-foreground capitalize">
                      {action}
                    </p>
                  </div>
                </CardContent>
              </Card>
            )
          }
        )}
      </div>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="flex items-center gap-2">
            <Calendar className="h-5 w-5" />
            Activity Log
          </CardTitle>
          <div className="flex items-center gap-3">
            <Select value={actionFilter} onValueChange={setActionFilter}>
              <SelectTrigger className="w-40">
                <Filter className="mr-2 h-4 w-4" />
                <SelectValue placeholder="Filter" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Events</SelectItem>
                <SelectItem value="granted">Granted</SelectItem>
                <SelectItem value="revoked">Revoked</SelectItem>
                <SelectItem value="accessed">Accessed</SelectItem>
                <SelectItem value="updated">Updated</SelectItem>
              </SelectContent>
            </Select>
            <Tabs
              value={viewMode}
              onValueChange={(v) => setViewMode(v as "timeline" | "table")}
            >
              <TabsList>
                <TabsTrigger value="timeline">Timeline</TabsTrigger>
                <TabsTrigger value="table">Table</TabsTrigger>
              </TabsList>
            </Tabs>
          </div>
        </CardHeader>
        <CardContent>
          {viewMode === "timeline" ? (
            <div className="space-y-0">
              {filteredHistory.map((event) => (
                <TimelineItem key={event.id} event={event} />
              ))}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Organization</TableHead>
                  <TableHead>Data Type</TableHead>
                  <TableHead>Action</TableHead>
                  <TableHead>Purpose</TableHead>
                  <TableHead>Timestamp</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredHistory.map((event) => (
                  <TableRow key={event.id}>
                    <TableCell className="font-medium">
                      <div className="flex items-center gap-2">
                        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-sm font-semibold text-primary">
                          {event.organizationLogo}
                        </div>
                        {event.organizationName}
                      </div>
                    </TableCell>
                    <TableCell>{event.dataType}</TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={cn("text-xs", actionColors[event.action])}
                      >
                        {actionLabels[event.action]}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {event.purpose}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {format(new Date(event.timestamp), "MMM d, yyyy HH:mm")}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
