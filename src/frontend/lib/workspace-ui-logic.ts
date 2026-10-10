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
