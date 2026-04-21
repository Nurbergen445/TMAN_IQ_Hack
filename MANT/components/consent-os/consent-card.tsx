"use client"

import { useState } from "react"
import { format } from "date-fns"
import { X } from "lucide-react"
import { Button } from "@/components/ui/button"
import { RevokeModal } from "./revoke-modal"
import type { Consent } from "@/lib/types"

interface ConsentCardProps {
  consent: Consent
  onRevoke?: (id: string) => void
}

export function ConsentCard({ consent, onRevoke }: ConsentCardProps) {
  const [revokeModalOpen, setRevokeModalOpen] = useState(false)

  return (
    <>
      <div className="flex items-center justify-between rounded-lg border border-border bg-card p-4 transition-colors hover:bg-muted/50">
        <div className="flex items-center gap-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 font-semibold text-primary">
            {consent.organizationName.charAt(0)}
          </div>
          <div>
            <p className="font-medium">{consent.organizationName}</p>
            <p className="text-sm text-muted-foreground">
              {consent.dataType} • {consent.purpose}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-4">
          <p className="text-sm text-muted-foreground">
            Granted {format(new Date(consent.grantedAt), "MMM d, yyyy")}
          </p>
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
            onClick={() => setRevokeModalOpen(true)}
          >
            <X className="h-4 w-4" />
            <span className="sr-only">Revoke consent</span>
          </Button>
        </div>
      </div>

      <RevokeModal
        open={revokeModalOpen}
        onOpenChange={setRevokeModalOpen}
        organizationName={consent.organizationName}
        dataTypes={[consent.dataType]}
        onConfirm={() => onRevoke?.(consent.id)}
      />
    </>
  )
}
