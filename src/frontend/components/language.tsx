"use client";

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { ApiError } from "@/lib/api";

export type Language = "en" | "bn";

type LanguageContextValue = {
  language: Language;
  setLanguage: (language: Language) => void;
  text: (english: string, bangla: string) => string;
  error: (caught: unknown, fallback: string) => string;
};

const LanguageContext = createContext<LanguageContextValue | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, setLanguage] = useState<Language>("en");

  useEffect(() => {
    const saved = window.localStorage.getItem("primetect-language");
    if (saved === "en" || saved === "bn") setLanguage(saved);
  }, []);

  useEffect(() => {
    window.localStorage.setItem("primetect-language", language);
  }, [language]);

  const value = useMemo<LanguageContextValue>(() => ({
    language,
    setLanguage,
    text: (english, bangla) => language === "bn" ? bangla : english,
    error: (caught, fallback) => localizeError(caught, fallback, language),
  }), [language]);

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useLanguage() {
  const value = useContext(LanguageContext);
  if (!value) throw new Error("useLanguage must be used inside LanguageProvider");
  return value;
}

export function localizeError(caught: unknown, fallback: string, language: Language) {
  if (language === "en") return caught instanceof ApiError ? caught.message : fallback;

  if (caught instanceof ApiError) {
    if (caught.status === 401) return "আপনার সাইন-ইন সেশন শেষ হয়েছে। আবার সাইন-ইন করুন।";
    if (caught.status === 403 && caught.message.includes("Another authorized person")) return "অন্য একজন অনুমোদিত ব্যক্তি আপনার রিপোর্ট অনুমোদন করবেন।";
    if (caught.status === 403) return "এই কাজটি করার অনুমতি আপনার নেই বা সম্পত্তিটি আপনার জন্য নির্ধারিত নয়।";
    if (caught.status === 409) return "তথ্যটি অন্য কেউ পরিবর্তন করেছেন। নতুন তথ্য লোড করে আবার চেষ্টা করুন।";
    if (caught.status === 503 && caught.message.includes("Room/meter issue storage")) return "রুম বা মিটারের সমস্যা রিপোর্টিং চালু করতে এই ডেটাবেসে প্রয়োজনীয় মাইগ্রেশন লাগবে।";
  }

  const messages: Record<string, string> = {
    "Could not load this billing month.": "এই বিলের মাস লোড করা যায়নি।",
    "Could not generate this billing month.": "এই বিলের মাসের খসড়া তৈরি করা যায়নি।",
    "The reading was not sent. Your input remains here; refresh if another user changed the draft.": "রিডিং পাঠানো যায়নি। আপনার লেখা এখানে আছে; অন্য কেউ খসড়া বদলালে নতুন করে লোড করুন।",
    "This month changed or could not be updated. Refresh and try again.": "এই মাসের তথ্য বদলে গেছে বা সংরক্ষণ করা যায়নি। নতুন করে লোড করে আবার চেষ্টা করুন।",
    "Could not load approved bills and collection reports.": "অনুমোদিত বিল ও পাওয়ার রিপোর্ট লোড করা যায়নি।",
    "Could not save the collection report.": "পাওয়ার রিপোর্ট সংরক্ষণ করা যায়নি।",
    "The report was not saved. Your entered values remain here.": "রিপোর্ট সংরক্ষণ করা যায়নি। আপনার লেখা তথ্য এখানেই আছে।",
    "Could not load the collection overview.": "পাওয়ার রিপোর্টের সারসংক্ষেপ লোড করা যায়নি।",
    "Could not load room or meter issues.": "রুম বা মিটারের সমস্যার রিপোর্ট লোড করা যায়নি।",
    "Could not load the room reading overview.": "রুমের রিডিং overview লোড করা যায়নি। আবার চেষ্টা করুন।",
    "The issue report was not sent. No billing data was changed.": "সমস্যার রিপোর্ট পাঠানো যায়নি। কোনো বিলিং তথ্য বদলায়নি।",
    "This issue changed since it was loaded. Refresh before trying again.": "সমস্যাটি লোড করার পর বদলেছে। আবার চেষ্টা করার আগে নতুন করে লোড করুন।",
    "This collection or reversal changed since it was loaded. Refresh before trying again.": "এই পাওয়ার রিপোর্ট বা ফেরত পরিবর্তন হয়েছে। আবার চেষ্টা করার আগে নতুন করে লোড করুন।",
    "Could not load collection review.": "পাওয়ার রিপোর্ট পর্যালোচনা লোড করা যায়নি।",
    "The collection changed or could not be updated. Refresh and try again.": "পাওয়ার রিপোর্ট বদলে গেছে বা আপডেট করা যায়নি। নতুন করে লোড করে আবার চেষ্টা করুন।",
    "This reversal changed concurrently or could not be updated. Refresh the queue and try again.": "এই ফেরত একই সময়ে বদলে গেছে বা আপডেট করা যায়নি। তালিকা নতুন করে লোড করে আবার চেষ্টা করুন।",
    "Permissions were not saved. Check the dependency explanation and try again.": "অনুমতিগুলো সংরক্ষণ করা যায়নি। নির্ভরতার ব্যাখ্যা দেখে আবার চেষ্টা করুন।",
    "The request could not be completed.": "অনুরোধটি সম্পন্ন করা যায়নি।",
  };
  return messages[fallback] ?? fallback;
}
