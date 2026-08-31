export interface BirthdayDateDocument {
  readonly day: number;
  readonly month: number;
  readonly year: number;
  readonly correctionCount: number;
  readonly lastBirthdayGiftYear: number;
}

export interface BirthdayAccountDocument {
  readonly birthday: BirthdayDateDocument;
  readonly earnedCosmeticIds: readonly string[];
}

export interface BirthdayGiftWindow {
  readonly active: boolean;
  readonly giftYear: number;
  readonly startUtc: string;
  readonly endExclusiveUtc: string;
}

export interface BirthdayGiftResult {
  readonly granted: boolean;
  readonly reason: "granted" | "outside-window" | "already-claimed";
  readonly giftYear: number;
  readonly cosmeticId: string;
  readonly purchasePromptAllowed: false;
}

export interface BirthdayGiftStore {
  runTransaction<T>(
    uid: string,
    operation: (
      current: BirthdayAccountDocument,
    ) => { readonly result: T; readonly next?: BirthdayAccountDocument },
  ): Promise<T>;
}

export interface BirthdayGiftRequest {
  readonly auth: { readonly uid: string } | null;
  readonly data: unknown;
}

export interface BirthdayGiftDependencies {
  readonly store: BirthdayGiftStore;
  readonly serverNow: () => Date;
}

const WINDOW_DAYS = 30;

export function nextBirthdayGiftRevision(currentRevision: number): number {
  if (!Number.isSafeInteger(currentRevision) || currentRevision < 0 ||
      currentRevision >= Number.MAX_SAFE_INTEGER) {
    throw new Error("Cloud save revision is invalid.");
  }
  return currentRevision + 1;
}

export function resolveBirthdayGiftWindow(
  birthday: Pick<BirthdayDateDocument, "day" | "month" | "year">,
  trustedNow: Date,
): BirthdayGiftWindow {
  requireDate(trustedNow, "Trusted server time");
  requireBirthday(birthday);
  const today = utcDate(trustedNow);
  let start = giftAnniversary(birthday, today.getUTCFullYear());
  if (today.getTime() < start.getTime()) {
    start = giftAnniversary(birthday, today.getUTCFullYear() - 1);
  }
  const end = addUtcDays(start, WINDOW_DAYS);
  return {
    active: today.getTime() >= start.getTime() && today.getTime() < end.getTime(),
    giftYear: start.getUTCFullYear(),
    startUtc: formatUtcDate(start),
    endExclusiveUtc: formatUtcDate(end),
  };
}

export async function claimBirthdayGiftForAccount(
  uid: string,
  store: BirthdayGiftStore,
  trustedNow: Date,
): Promise<BirthdayGiftResult> {
  if (!uid.trim()) {
    throw new Error("An authenticated account is required.");
  }
  requireDate(trustedNow, "Trusted server time");

  return store.runTransaction(uid, (current) => {
    const window = resolveBirthdayGiftWindow(current.birthday, trustedNow);
    if (!window.active) {
      return {
        result: result(false, "outside-window", 0, ""),
      };
    }
    if (current.birthday.lastBirthdayGiftYear >= window.giftYear) {
      return {
        result: result(false, "already-claimed", window.giftYear, ""),
      };
    }

    const cosmeticId = `birthday.ori-starlight.${window.giftYear}`;
    const earnedCosmeticIds = Array.from(
      new Set([...current.earnedCosmeticIds, cosmeticId]),
    );
    const next: BirthdayAccountDocument = {
      ...current,
      birthday: {
        ...current.birthday,
        lastBirthdayGiftYear: window.giftYear,
      },
      earnedCosmeticIds,
    };
    return {
      result: result(true, "granted", window.giftYear, cosmeticId),
      next,
    };
  });
}

export async function handleBirthdayGiftRequest(
  request: BirthdayGiftRequest,
  dependencies: BirthdayGiftDependencies,
): Promise<BirthdayGiftResult> {
  const uid = request.auth?.uid;
  if (!uid) {
    throw new Error("An authenticated account is required.");
  }

  // Deliberately ignore request.data: eligibility is derived exclusively from
  // the authenticated account document and trusted server time.
  return claimBirthdayGiftForAccount(
    uid,
    dependencies.store,
    dependencies.serverNow(),
  );
}

function result(
  granted: boolean,
  reason: BirthdayGiftResult["reason"],
  giftYear: number,
  cosmeticId: string,
): BirthdayGiftResult {
  return {
    granted,
    reason,
    giftYear,
    cosmeticId,
    purchasePromptAllowed: false,
  };
}

function requireBirthday(
  birthday: Pick<BirthdayDateDocument, "day" | "month" | "year">,
): void {
  if (!Number.isInteger(birthday.day) ||
      !Number.isInteger(birthday.month) ||
      !Number.isInteger(birthday.year)) {
    throw new Error("Stored birthday is invalid.");
  }
  const date = new Date(Date.UTC(birthday.year, birthday.month - 1, birthday.day));
  if (date.getUTCFullYear() !== birthday.year ||
      date.getUTCMonth() !== birthday.month - 1 ||
      date.getUTCDate() !== birthday.day) {
    throw new Error("Stored birthday is invalid.");
  }
}

function requireDate(value: Date, label: string): void {
  if (!(value instanceof Date) || !Number.isFinite(value.getTime())) {
    throw new Error(`${label} is invalid.`);
  }
}

function giftAnniversary(
  birthday: Pick<BirthdayDateDocument, "day" | "month">,
  year: number,
): Date {
  if (birthday.month === 2 && birthday.day === 29 && !isLeapYear(year)) {
    return new Date(Date.UTC(year, 1, 28));
  }
  return new Date(Date.UTC(year, birthday.month - 1, birthday.day));
}

function isLeapYear(year: number): boolean {
  return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
}

function utcDate(value: Date): Date {
  return new Date(Date.UTC(
    value.getUTCFullYear(),
    value.getUTCMonth(),
    value.getUTCDate(),
  ));
}

function addUtcDays(value: Date, days: number): Date {
  const result = new Date(value.getTime());
  result.setUTCDate(result.getUTCDate() + days);
  return result;
}

function formatUtcDate(value: Date): string {
  return value.toISOString().slice(0, 10);
}
