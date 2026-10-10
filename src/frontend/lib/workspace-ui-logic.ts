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
