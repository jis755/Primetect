export function nextMeterRoomIndex(currentIndex: number, roomCount: number, unfinishedOnly: boolean) {
  if (roomCount <= 0 || unfinishedOnly) return Math.max(0, Math.min(currentIndex, Math.max(0, roomCount - 1)));
  return Math.min(currentIndex + 1, Math.max(0, roomCount - 1));
}

export function reportableCollectionAmount(raw: string, remaining: number | null) {
  const normalized = raw.replace(/[০-৯]/g, (digit) => String("০১২৩৪৫৬৭৮৯".indexOf(digit))).trim();
  if (!normalized || remaining === null) return { valid: false, amount: null };
  const amount = Number(normalized);
  return { valid: Number.isFinite(amount) && amount > 0 && amount <= remaining, amount: Number.isFinite(amount) ? amount : null };
}

export function preserveRoomDraftAfterFailure(currentReading: string, observationDate: string) {
  return { currentReading, observationDate };
}

export type MeterProgressUnit = {
  electricityApplicable: boolean;
  readingStatus: string;
};

export function summarizeMeterProgress(units: MeterProgressUnit[]) {
  const applicable = units.filter((unit) => unit.electricityApplicable);
  const missing = applicable.filter((unit) => unit.readingStatus === "NoDraft" || unit.readingStatus === "Missing").length;
  return {
    applicable: applicable.length,
    entered: applicable.length - missing,
    missing,
    savedDraft: applicable.filter((unit) => unit.readingStatus === "SavedDraft").length,
    awaitingReview: applicable.filter((unit) => unit.readingStatus === "AwaitingReview").length,
    returned: applicable.filter((unit) => unit.readingStatus === "Returned").length,
    approved: applicable.filter((unit) => unit.readingStatus === "Approved").length,
  };
}

export type StaffTaskPermissions = {
  viewMeterReadings: boolean;
  reportCollections: boolean;
  reportRoomMeterIssues: boolean;
};

export function visibleStaffTasks(permissions: StaffTaskPermissions) {
  return {
    meter: permissions.viewMeterReadings,
    collections: permissions.reportCollections,
    feedback: permissions.reportRoomMeterIssues,
    rooms: permissions.viewMeterReadings,
  };
}

export function monthlySubmissionLocation(location: "entry" | "overview") {
  return location === "overview";
}

export function collectionBillDisplay(billedAmount: number | null) {
  return billedAmount === null ? "No approved bill" : `Tk ${billedAmount.toFixed(2)} billed`;
}

export function preserveWorkflowContext<T extends { propertyId: string; billingMonth: string; unitId?: string; language: "en" | "bn" }, L extends "en" | "bn">(context: T, language: L): Omit<T, "language"> & { language: L } {
  return { ...context, language } as Omit<T, "language"> & { language: L };
}

export function adoptMeterOcrSuggestion(suggestion: number) {
  return String(suggestion);
}

export function isCurrentMeterPhotoResult(requestId: number, currentRequestId: number, returnedCycleId: string, expectedCycleId: string, returnedBillId: string, expectedBillId: string) {
  return requestId === currentRequestId && returnedCycleId === expectedCycleId && returnedBillId === expectedBillId;
}

export function meterPreviousReadingState(reading: number | null, source: "ApprovedPeriod" | "OpeningEntry" | "Missing", date: string | null) {
  if (reading === null) return { kind: "unavailable" as const, date: null };
  return { kind: source === "OpeningEntry" ? "opening" as const : "approved" as const, date };
}

export function meterPhotoConfirmationRequired(hasPhoto: boolean, confirmed: boolean) {
  return hasPhoto && !confirmed;
}

export function meterRoomAdvanceAfterSave(saved: boolean, currentIndex: number, roomCount: number, unfinishedOnly: boolean) {
  return saved ? nextMeterRoomIndex(currentIndex, roomCount, unfinishedOnly) : nextMeterRoomIndex(currentIndex, roomCount, true);
}

export type RegistrationFields = { fullName: string; email: string; password: string; confirmPassword: string; organizationName: string };

export function registrationStepOneValid(fields: Pick<RegistrationFields, "fullName" | "email" | "password" | "confirmPassword">) {
  return fields.fullName.trim().length > 0
    && fields.email.trim().length > 0
    && fields.password.length > 0
    && fields.password === fields.confirmPassword;
}

export function registrationReady(fields: RegistrationFields) {
  return registrationStepOneValid(fields) && fields.organizationName.trim().length > 0;
}

export function authPanelTitle(mode: "login" | "register" | "accept", language: "en" | "bn") {
  const titles = {
    login: { en: "Log in", bn: "সাইন ইন" },
    register: { en: "Create account", bn: "অ্যাকাউন্ট তৈরি করুন" },
    accept: { en: "Accept staff invitation", bn: "স্টাফ আমন্ত্রণ গ্রহণ করুন" },
  } as const;
  return titles[mode][language];
}

export type MeterSegmentLike = { sortOrder: number; previousReading: number | null; currentReading: number | null; consumptionUnits: number | null; finalReadingUnavailable: boolean };

export function meterSegmentLabel(segment: MeterSegmentLike) {
  if (segment.consumptionUnits !== null) return `${segment.consumptionUnits} units`;
  if (segment.finalReadingUnavailable) return "old final reading unavailable";
  return "incomplete";
}

export function replacementOpeningReadingValid(raw: string) {
  const normalized = raw.replace(/[০-৯]/g, (digit) => String("০১২৩৪৫৬৭৮৯".indexOf(digit))).trim();
  if (!normalized) return { valid: false as const, reason: "missing" as const, value: null };
  const value = Number(normalized);
  if (!Number.isFinite(value) || value < 0) return { valid: false as const, reason: "invalid" as const, value: null };
  return { valid: true as const, reason: "ok" as const, value };
}

const replacementGuardTranslations: Record<string, string> = {
  "Return the submitted billing month for correction before replacing this meter.": "মিটার প্রতিস্থাপনের আগে পাঠানো বিলের মাস সংশোধনের জন্য ফেরত দিন।",
  "This replacement date would reinterpret an approved billing period. Choose a date after the last approved month.": "এই প্রতিস্থাপনের তারিখ অনুমোদিত বিলের সময়কে নতুন করে ব্যাখ্যা করবে। শেষ অনুমোদিত মাসের পরে একটি তারিখ বেছে নিন।",
  "This submeter is no longer the active meter for the room. Refresh before replacing it.": "এই সাবমিটারটি এখন আর এই রুমের সক্রিয় মিটার নয়। প্রতিস্থাপনের আগে নতুন করে লোড করুন।",
  "This submeter changed since it was loaded. Refresh before replacing it.": "লোড করার পর এই সাবমিটার বদলে গেছে। প্রতিস্থাপনের আগে নতুন করে লোড করুন।",
  "This meter photo was captured for a replaced meter. Capture a new photo for the current submeter before saving.": "এই ছবিটি প্রতিস্থাপিত মিটারের জন্য তোলা হয়েছিল। সংরক্ষণের আগে বর্তমান সাবমিটারের নতুন ছবি তুলুন।",
  "The replacement could not be persisted atomically. No change was recorded.": "প্রতিস্থাপন একসাথে সংরক্ষণ করা যায়নি। কোনো পরিবর্তন রেকর্ড হয়নি।",
};

export function replacementGuardMessage(serverMessage: string, language: "en" | "bn") {
  if (language === "en") return serverMessage;
  return replacementGuardTranslations[serverMessage] ?? serverMessage;
}

export function hasReplacementGuardTranslation(serverMessage: string) {
  return Object.prototype.hasOwnProperty.call(replacementGuardTranslations, serverMessage);
}
