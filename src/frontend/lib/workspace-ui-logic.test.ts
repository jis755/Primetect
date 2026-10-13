import assert from "node:assert/strict";
import test from "node:test";
import { adoptMeterOcrSuggestion, authPanelTitle, collectionBillDisplay, hasReplacementGuardTranslation, isCurrentMeterPhotoResult, meterPhotoConfirmationRequired, meterPreviousReadingState, meterRoomAdvanceAfterSave, meterSegmentLabel, monthlySubmissionLocation, nextMeterRoomIndex, preserveRoomDraftAfterFailure, preserveWorkflowContext, registrationReady, registrationStepOneValid, replacementGuardMessage, replacementOpeningReadingValid, reportableCollectionAmount, summarizeMeterProgress, visibleStaffTasks } from "./workspace-ui-logic.ts";

test("room entry advances only after a successful save and stops at the last room", () => {
  assert.equal(nextMeterRoomIndex(0, 3, false), 1);
  assert.equal(nextMeterRoomIndex(2, 3, false), 2);
  assert.equal(nextMeterRoomIndex(0, 3, true), 0);
});

test("whole and decimal collection amounts use the current remaining balance", () => {
  assert.deepEqual(reportableCollectionAmount("199", 200), { valid: true, amount: 199 });
  assert.deepEqual(reportableCollectionAmount("১৯৯.০০", 200), { valid: true, amount: 199 });
  assert.equal(reportableCollectionAmount("200.01", 200).valid, false);
  assert.equal(reportableCollectionAmount("199", null).valid, false);
});

test("failed room saves preserve the entered reading and observation date", () => {
  assert.deepEqual(preserveRoomDraftAfterFailure("10040", "2026-02-01"), { currentReading: "10040", observationDate: "2026-02-01" });
});

test("counts only applicable units and keeps review states distinct", () => {
  assert.deepEqual(summarizeMeterProgress([
    { electricityApplicable: true, readingStatus: "NoDraft" },
    { electricityApplicable: true, readingStatus: "SavedDraft" },
    { electricityApplicable: true, readingStatus: "AwaitingReview" },
    { electricityApplicable: true, readingStatus: "Returned" },
    { electricityApplicable: true, readingStatus: "Approved" },
    { electricityApplicable: false, readingStatus: "NotApplicable" },
  ]), { applicable: 5, entered: 4, missing: 1, savedDraft: 1, awaitingReview: 1, returned: 1, approved: 1 });
});

test("staff task cards follow effective permissions", () => {
  assert.deepEqual(visibleStaffTasks({ viewMeterReadings: true, reportCollections: false, reportRoomMeterIssues: true }), {
    meter: true,
    collections: false,
    feedback: true,
    rooms: true,
  });
  assert.deepEqual(visibleStaffTasks({ viewMeterReadings: false, reportCollections: true, reportRoomMeterIssues: false }), {
    meter: false,
    collections: true,
    feedback: false,
    rooms: false,
  });
  assert.deepEqual(visibleStaffTasks({ viewMeterReadings: false, reportCollections: false, reportRoomMeterIssues: false }), {
    meter: false,
    collections: false,
    feedback: false,
    rooms: false,
  });
});

test("monthly submission is available only from the overview", () => {
  assert.equal(monthlySubmissionLocation("entry"), false);
  assert.equal(monthlySubmissionLocation("overview"), true);
});

test("language changes preserve property, month, and room context", () => {
  const context = { propertyId: "property-1", billingMonth: "2026-10", unitId: "unit-6b", language: "en" as const };
  assert.deepEqual(preserveWorkflowContext(context, "bn"), { ...context, language: "bn" });
});

test("missing approved bills remain distinct from zero-valued bills", () => {
  assert.equal(collectionBillDisplay(null), "No approved bill");
  assert.equal(collectionBillDisplay(0), "Tk 0.00 billed");
});

test("OCR suggestion adoption is explicit and stale photo responses are rejected", () => {
  assert.equal(adoptMeterOcrSuggestion(1234.5), "1234.5");
  assert.equal(isCurrentMeterPhotoResult(3, 3, "cycle-a", "cycle-a", "bill-a", "bill-a"), true);
  assert.equal(isCurrentMeterPhotoResult(2, 3, "cycle-a", "cycle-a", "bill-a", "bill-a"), false);
  assert.equal(isCurrentMeterPhotoResult(3, 3, "cycle-b", "cycle-a", "bill-a", "bill-a"), false);
});

test("previous-reading states do not invent approval history or dates", () => {
  assert.deepEqual(meterPreviousReadingState(10030, "ApprovedPeriod", "2026-09-01"), { kind: "approved", date: "2026-09-01" });
  assert.deepEqual(meterPreviousReadingState(0, "OpeningEntry", null), { kind: "opening", date: null });
  assert.deepEqual(meterPreviousReadingState(null, "Missing", null), { kind: "unavailable", date: null });
});

test("a meter photo requires explicit confirmation before saving", () => {
  assert.equal(meterPhotoConfirmationRequired(true, false), true);
  assert.equal(meterPhotoConfirmationRequired(true, true), false);
  assert.equal(meterPhotoConfirmationRequired(false, false), false);
});

test("failed saves never advance to the next room and successful saves do", () => {
  assert.equal(meterRoomAdvanceAfterSave(false, 1, 5, false), 1);
  assert.equal(meterRoomAdvanceAfterSave(false, 4, 5, false), 4);
  assert.equal(meterRoomAdvanceAfterSave(true, 1, 5, false), 2);
  assert.equal(meterRoomAdvanceAfterSave(true, 4, 5, false), 4);
  assert.equal(meterRoomAdvanceAfterSave(true, 0, 3, true), 0);
});

test("replacement opening reading keeps zero distinct from missing input", () => {
  assert.deepEqual(replacementOpeningReadingValid("0"), { valid: true, reason: "ok", value: 0 });
  assert.deepEqual(replacementOpeningReadingValid("35"), { valid: true, reason: "ok", value: 35 });
  assert.deepEqual(replacementOpeningReadingValid("০"), { valid: true, reason: "ok", value: 0 });
  assert.equal(replacementOpeningReadingValid("").valid, false);
  assert.equal(replacementOpeningReadingValid("   ").reason, "missing");
  assert.equal(replacementOpeningReadingValid("-1").reason, "invalid");
  assert.equal(replacementOpeningReadingValid("abc").reason, "invalid");
});

test("replacement segments are described per installation and never cross-subtracted", () => {
  assert.equal(meterSegmentLabel({ sortOrder: 0, previousReading: 10020, currentReading: 10040, consumptionUnits: 20, finalReadingUnavailable: false }), "20 units");
  assert.equal(meterSegmentLabel({ sortOrder: 1, previousReading: 35, currentReading: 50, consumptionUnits: 15, finalReadingUnavailable: false }), "15 units");
  assert.equal(meterSegmentLabel({ sortOrder: 0, previousReading: 10020, currentReading: null, consumptionUnits: null, finalReadingUnavailable: true }), "old final reading unavailable");
  assert.equal(meterSegmentLabel({ sortOrder: 1, previousReading: 35, currentReading: null, consumptionUnits: null, finalReadingUnavailable: false }), "incomplete");
});

test("submitted-month replacement rejection is bilingual", () => {
  const message = "Return the submitted billing month for correction before replacing this meter.";
  assert.equal(replacementGuardMessage(message, "en"), message);
  assert.equal(replacementGuardMessage(message, "bn"), "মিটার প্রতিস্থাপনের আগে পাঠানো বিলের মাস সংশোধনের জন্য ফেরত দিন।");
  assert.equal(hasReplacementGuardTranslation(message), true);
  assert.equal(replacementGuardMessage("Some other message", "bn"), "Some other message");
});

test("auth panel navigation labels are bilingual", () => {
  assert.equal(authPanelTitle("login", "en"), "Log in");
  assert.equal(authPanelTitle("register", "bn"), "অ্যাকাউন্ট তৈরি করুন");
  assert.equal(authPanelTitle("accept", "en"), "Accept staff invitation");
  assert.equal(authPanelTitle("accept", "bn"), "স্টাফ আমন্ত্রণ গ্রহণ করুন");
});

test("two-step registration validates account details then requires an organization name", () => {
  const partial = { fullName: "Amina", email: "amina@example.test", password: "StrongPass!1", confirmPassword: "StrongPass!1", organizationName: "" };
  assert.equal(registrationStepOneValid(partial), true);
  assert.equal(registrationReady(partial), false); // step 2 still needs the organization name
  assert.equal(registrationReady({ ...partial, organizationName: "Northstar" }), true);
  assert.equal(registrationStepOneValid({ ...partial, confirmPassword: "different" }), false);
  assert.equal(registrationStepOneValid({ ...partial, password: "", confirmPassword: "" }), false);
  assert.equal(registrationStepOneValid({ ...partial, fullName: "   " }), false);
  assert.equal(registrationStepOneValid({ ...partial, email: "  " }), false);
});
