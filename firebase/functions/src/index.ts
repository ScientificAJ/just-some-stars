import { randomUUID } from "node:crypto";

import { getApps, initializeApp } from "firebase-admin/app";
import { getAuth } from "firebase-admin/auth";
import { FieldValue, getFirestore } from "firebase-admin/firestore";
import * as functionsV1 from "firebase-functions/v1";
import { HttpsError, onCall } from "firebase-functions/v2/https";

import {
  type BirthdayAccountDocument,
  type BirthdayGiftStore,
  handleBirthdayGiftRequest,
  nextBirthdayGiftRevision,
} from "./birthdayGift.js";

if (getApps().length === 0) {
  initializeApp();
}

const firestore = getFirestore();

const store: BirthdayGiftStore = {
  async runTransaction<T>(
    uid: string,
    operation: (
      current: BirthdayAccountDocument,
    ) => { readonly result: T; readonly next?: BirthdayAccountDocument },
  ): Promise<T> {
    const reference = firestore.doc(`users/${uid}`);
    return firestore.runTransaction(async (transaction) => {
      const snapshot = await transaction.get(reference);
      if (!snapshot.exists) {
        throw new HttpsError("failed-precondition", "Account backup is missing.");
      }

      const data = snapshot.data();
      const nextRevision = nextBirthdayGiftRevision(data?.revision);
      const save = data?.save as Record<string, unknown> | undefined;
      const birthday = save?.birthday as
        BirthdayAccountDocument["birthday"] | undefined;
      const earnedCosmeticIds = save?.earnedCosmeticIds;
      if (!birthday || !Array.isArray(earnedCosmeticIds)) {
        throw new HttpsError("failed-precondition", "Account birthday is missing.");
      }
      const rawClaimedYears = data?.birthdayGiftYears;
      if (rawClaimedYears !== undefined &&
          (!Array.isArray(rawClaimedYears) ||
            rawClaimedYears.some((value) => !Number.isInteger(value)))) {
        throw new HttpsError(
          "failed-precondition",
          "Account birthday claim history is invalid.",
        );
      }

      const current: BirthdayAccountDocument = {
        birthday,
        earnedCosmeticIds: earnedCosmeticIds.filter(
          (value): value is string => typeof value === "string",
        ),
      };
      const mutation = operation(current);
      if (mutation.next) {
        const giftYear = mutation.next.birthday.lastBirthdayGiftYear;
        const claimedYears =
          (rawClaimedYears as number[] | undefined) ?? [];
        if (claimedYears.includes(giftYear)) {
          throw new HttpsError(
            "already-exists",
            "This account already received its birthday gift for that year.",
          );
        }
        const cosmeticId = mutation.next.earnedCosmeticIds.find(
          (value) => value === `birthday.ori-starlight.${giftYear}`,
        );
        if (!cosmeticId) {
          throw new HttpsError("failed-precondition", "Gift result is invalid.");
        }
        transaction.update(reference, {
          revision: nextRevision,
          clientWriteId: randomUUID().replaceAll("-", ""),
          birthdayGiftYears: [...claimedYears, giftYear],
          "save.metadata.revision": nextRevision,
          "save.birthday.lastBirthdayGiftYear":
            mutation.next.birthday.lastBirthdayGiftYear,
          "save.earnedCosmeticIds": mutation.next.earnedCosmeticIds,
          updatedAt: FieldValue.serverTimestamp(),
        });
      }
      return mutation.result;
    });
  },
};

export const claimBirthdayGift = onCall(
  { enforceAppCheck: true },
  async (request) => {
    try {
      return await handleBirthdayGiftRequest(
        {
          auth: request.auth ? { uid: request.auth.uid } : null,
          data: request.data,
        },
        { store, serverNow: () => new Date() },
      );
    } catch (error) {
      if (error instanceof HttpsError) {
        throw error;
      }
      const message = error instanceof Error ? error.message : "Gift claim failed.";
      if (/authenticated/i.test(message)) {
        throw new HttpsError("unauthenticated", message);
      }
      throw new HttpsError("failed-precondition", message);
    }
  },
);

export const deleteCaptainAccount = onCall(
  { enforceAppCheck: true },
  async (request) => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError(
        "unauthenticated",
        "An authenticated account is required.",
      );
    }

    // Authentication is removed first so the same UID cannot recreate its
    // server-owned annual-claim ledger. The Auth deletion trigger below is the
    // retryable cleanup authority; this direct delete makes the callable's
    // successful response mean the private root is already gone.
    await getAuth().deleteUser(uid);
    await firestore.doc(`users/${uid}`).delete();
    return { deleted: true };
  },
);

export const cleanupDeletedCaptainAccount = functionsV1
  .runWith({ failurePolicy: true })
  .auth.user()
  .onDelete(async (user) => {
    await firestore.doc(`users/${user.uid}`).delete();
  });
