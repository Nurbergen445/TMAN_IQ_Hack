"use client"

import { AlertTriangle } from "lucide-react"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"

interface RevokeModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  organizationName: string
  dataTypes: string[]
  onConfirm: () => void
}

export function RevokeModal({
  open,
  onOpenChange,
  organizationName,
  dataTypes,
  onConfirm,
}: RevokeModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-destructive/10">
            <AlertTriangle className="h-6 w-6 text-destructive" />
          </div>
          <DialogTitle className="text-center">
            Revoke Access for {organizationName}?
          </DialogTitle>
          <DialogDescription className="text-center">
            This action will immediately revoke all data access permissions for this organization.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="rounded-lg bg-muted p-4">
            <p className="text-sm font-medium">Consequences of revoking access:</p>
            <ul className="mt-2 space-y-2 text-sm text-muted-foreground">
              <li className="flex items-start gap-2">
                <span className="mt-1 h-1.5 w-1.5 rounded-full bg-destructive" />
                You may lose access to personalized features
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-1 h-1.5 w-1.5 rounded-full bg-destructive" />
                Connected services may stop working
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-1 h-1.5 w-1.5 rounded-full bg-destructive" />
                Historical data may be deleted (30-day retention)
              </li>
            </ul>
          </div>

          <div className="rounded-lg border border-border p-4">
            <p className="text-sm font-medium">Data types that will be revoked:</p>
            <div className="mt-2 flex flex-wrap gap-2">
              {dataTypes.map((type) => (
                <span
                  key={type}
                  className="rounded-full bg-secondary px-2.5 py-1 text-xs font-medium"
                >
                  {type}
                </span>
              ))}
            </div>
          </div>
        </div>

        <DialogFooter className="gap-2 sm:gap-0">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            variant="destructive"
            onClick={() => {
              onConfirm()
              onOpenChange(false)
            }}
          >
            Revoke Access
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
