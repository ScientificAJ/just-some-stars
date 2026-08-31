import assert from "node:assert/strict";
import test from "node:test";

import {
  type BirthdayAccountDocument,
  type BirthdayGiftStore,
  claimBirthdayGiftForAccount,
  handleBirthdayGiftRequest,
  nextBirthdayGiftRevision,
  resolveBirthdayGiftWindow,
} from "../src/birthdayGift.js";

function profile(overrides: Partial<BirthdayAccountDocument> = {}): BirthdayAccountDocument {
  return {
    birthday: {
      day: 4,
      month: 7,
      year: 2013,
      correctionCount: 0,
      lastBirthdayGiftYear: 0,
    },
    earnedCosmeticIds: [],
    ...overrides,
  };
}

class MemoryBirthdayGiftStore implements BirthdayGiftStore {
  private tail: Promise<void> = Promise.resolve();
  public value: BirthdayAccountDocument;

  public constructor(initial: BirthdayAccountDocument) {
    this.value = structuredClone(initial);
  }

  public async runTransaction<T>(
    uid: string,
    operation: (
      current: BirthdayAccountDocument,
    ) => { readonly result: T; readonly next?: BirthdayAccountDocument },
  ): Promise<T> {
    assert.equal(uid, "firebase.uid.task22");
    const previous = this.tail;
    let release: () => void = () => undefined;
    this.tail = new Promise<void>((resolve) => {
      release = resolve;
    });
    await previous;
    try {
      const mutation = operation(structuredClone(this.value));
      if (mutation.next) {
        this.value = structuredClone(mutation.next);
      }
      return mutation.result;
    } finally {
      release();
    }
  }
}

test("leap-day gift uses February 28 in a non-leap year", () => {
  const window = resolveBirthdayGiftWindow(
    { day: 29, month: 2, year: 2012 },
    new Date("2025-02-28T00:00:00.000Z"),
  );

  assert.equal(window.active, true);
  assert.equal(window.giftYear, 2025);
  assert.equal(window.startUtc, "2025-02-28");
  assert.equal(window.endExclusiveUtc, "2025-03-30");
});

test("claim window is exactly thirty trusted UTC dates", () => {
  assert.equal(
    resolveBirthdayGiftWindow(
      { day: 4, month: 7, year: 2013 },
      new Date("2026-07-03T23:59:59.999Z"),
    ).active,
    false,
  );
  assert.equal(
    resolveBirthdayGiftWindow(
      { day: 4, month: 7, year: 2013 },
      new Date("2026-08-02T23:59:59.999Z"),
    ).active,
    true,
  );
  assert.equal(
    resolveBirthdayGiftWindow(
      { day: 4, month: 7, year: 2013 },
      new Date("2026-08-03T00:00:00.000Z"),
    ).active,
    false,
  );

  const yearCrossing = resolveBirthdayGiftWindow(
    { day: 20, month: 12, year: 2013 },
    new Date("2027-01-01T12:00:00.000Z"),
  );
  assert.equal(yearCrossing.active, true);
  assert.equal(yearCrossing.giftYear, 2026);
  assert.equal(yearCrossing.startUtc, "2026-12-20");
  assert.equal(yearCrossing.endExclusiveUtc, "2027-01-19");
});

test("authenticated transaction grants one annual gift under concurrent calls", async () => {
  const store = new MemoryBirthdayGiftStore(profile());
  const now = new Date("2026-07-04T12:00:00.000Z");

  const [first, second] = await Promise.all([
    claimBirthdayGiftForAccount("firebase.uid.task22", store, now),
    claimBirthdayGiftForAccount("firebase.uid.task22", store, now),
  ]);

  assert.deepEqual(
    [first.granted, second.granted].sort(),
    [false, true],
  );
  assert.equal(store.value.birthday.lastBirthdayGiftYear, 2026);
  assert.deepEqual(
    store.value.earnedCosmeticIds,
    ["birthday.ori-starlight.2026"],
  );
  assert.equal(first.purchasePromptAllowed, false);
  assert.equal(second.purchasePromptAllowed, false);
});

test("callable rejects guests and ignores caller-supplied date or birthday", async () => {
  const store = new MemoryBirthdayGiftStore(profile());
  const serverNow = new Date("2026-07-04T00:00:00.000Z");

  await assert.rejects(
    handleBirthdayGiftRequest(
      {
        auth: null,
        data: { now: "2099-07-04", birthday: "2000-01-01" },
      },
      { store, serverNow: () => serverNow },
    ),
    /authenticated/i,
  );

  const result = await handleBirthdayGiftRequest(
    {
      auth: { uid: "firebase.uid.task22" },
      data: { now: "2099-07-04", birthday: "2000-01-01" },
    },
    { store, serverNow: () => serverNow },
  );
  assert.equal(result.granted, true);
  assert.equal(result.giftYear, 2026);
});

test("outside-window and already-claimed paths do not mutate profile", async () => {
  const outsideStore = new MemoryBirthdayGiftStore(profile());
  const outside = await claimBirthdayGiftForAccount(
    "firebase.uid.task22",
    outsideStore,
    new Date("2026-06-01T12:00:00.000Z"),
  );
  assert.equal(outside.granted, false);
  assert.equal(outside.reason, "outside-window");
  assert.equal(outsideStore.value.birthday.lastBirthdayGiftYear, 0);

  const claimedStore = new MemoryBirthdayGiftStore(profile({
    birthday: {
      day: 4,
      month: 7,
      year: 2013,
      correctionCount: 0,
      lastBirthdayGiftYear: 2026,
    },
  }));
  const repeated = await claimBirthdayGiftForAccount(
    "firebase.uid.task22",
    claimedStore,
    new Date("2026-07-05T12:00:00.000Z"),
  );
  assert.equal(repeated.granted, false);
  assert.equal(repeated.reason, "already-claimed");
  assert.deepEqual(claimedStore.value.earnedCosmeticIds, []);
});

test("server birthday gift mutation advances the cloud CAS revision", () => {
  assert.equal(nextBirthdayGiftRevision(0), 1);
  assert.equal(nextBirthdayGiftRevision(41), 42);
  assert.throws(() => nextBirthdayGiftRevision(-1), /revision/i);
  assert.throws(() => nextBirthdayGiftRevision(Number.MAX_SAFE_INTEGER), /revision/i);
});
