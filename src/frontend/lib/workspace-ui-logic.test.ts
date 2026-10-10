import assert from "node:assert/strict";
import test from "node:test";
import { nextMeterRoomIndex, preserveRoomDraftAfterFailure, reportableCollectionAmount } from "./workspace-ui-logic.ts";

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
