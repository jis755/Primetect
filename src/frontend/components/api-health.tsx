"use client";

import { useEffect, useState } from "react";

type HealthState = "checking" | "online" | "offline";

export function ApiHealth() {
  const [state, setState] = useState<HealthState>("checking");

  useEffect(() => {
    const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

    fetch(`${baseUrl}/api/health`, { cache: "no-store" })
      .then((response) => {
        setState(response.ok ? "online" : "offline");
      })
      .catch(() => {
        setState("offline");
      });
  }, []);

  const label = state === "checking" ? "Checking API" : state === "online" ? "API online" : "API offline";
  const dotClass = state === "online" ? "bg-emerald-500" : state === "offline" ? "bg-rose-400" : "bg-sun";

  return (
    <span className="inline-flex items-center gap-2 rounded-full border border-pine/10 bg-white/70 px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-pine">
      <span className={`h-2 w-2 rounded-full ${dotClass}`} aria-hidden="true" />
      {label}
    </span>
  );
}
