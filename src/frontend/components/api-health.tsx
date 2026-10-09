"use client";

import { useEffect, useState } from "react";
import { useLanguage } from "@/components/language";

type HealthState = "checking" | "online" | "offline";

export function ApiHealth() {
  const [state, setState] = useState<HealthState>("checking");
  const { language, setLanguage, text } = useLanguage();

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

  const label = state === "checking"
    ? text("Checking API", "এপিআই দেখা হচ্ছে")
    : state === "online"
      ? text("API online", "এপিআই চালু")
      : text("API offline", "এপিআই বন্ধ");
  const dotClass = state === "online" ? "bg-emerald-500" : state === "offline" ? "bg-rose-400" : "bg-sun";

  return <span className="inline-flex items-center gap-2"><span className="inline-flex items-center gap-2 rounded-full border border-pine/10 bg-white/70 px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-pine"><span className={`h-2 w-2 rounded-full ${dotClass}`} aria-hidden="true" />{label}</span><button type="button" aria-label={text("Change language", "ভাষা পরিবর্তন করুন")} onClick={() => setLanguage(language === "en" ? "bn" : "en")} className="min-h-11 rounded-xl border border-ink/15 bg-white px-3 py-2 text-xs font-bold text-ink">{language === "en" ? "বাংলা" : "English"}</button></span>;
}
