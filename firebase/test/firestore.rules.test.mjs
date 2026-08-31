import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test, { after, before, beforeEach } from "node:test";
import { fileURLToPath } from "node:url";

import {
  assertFails,
  assertSucceeds,
  initializeTestEnvironment,
} from "@firebase/rules-unit-testing";
import {
  deleteDoc,
  doc,
  getDoc,
  getDocs,
  serverTimestamp,
  setDoc,
  collection,
} from "firebase/firestore";

const directory = path.dirname(fileURLToPath(import.meta.url));
const projectId = "jss-rules-test";
let environment;

function validSave(revision = 1) {
  return {
    schemaVersion: 3,
    story: {
      checkpointId: "story.prologue.start",
      checkpointOrdinal: 0,
    },
    mission: {
      missionId: "",
      checkpointNodeId: "",
      checkpointOrdinal: 0,
      completedNodeIds: [],
      activeNodeIds: [],
    },
    captain: {
      bodyFamilyId: "captain.family.a",
      appearancePresetId: "captain.face.01",
      suitCosmeticId: "suit.clubhouse",
      lastCustomizedUtcTicks: 100,
    },
    discoveryIds: ["discovery.mirra.signal"],
    earnedCosmeticIds: [],
    atlasEntryIds: ["atlas.mirra"],
    birthday: {
      hasValue: false,
      day: 0,
      month: 0,
      year: 0,
      correctionCount: 0,
      lastBirthdayGiftYear: 0,
    },
    metadata: {
      saveId: "save.fixture",
      revision,
      createdUtcTicks: 100,
      updatedUtcTicks: 100 + revision,
    },
  };
}

function validDocument(revision = 1, createdAt = serverTimestamp()) {
  return {
    documentSchemaVersion: 1,
    revision,
    clientWriteId: `write-${revision}`,
    createdAt,
    updatedAt: serverTimestamp(),
    save: validSave(revision),
  };
}

before(async () => {
  environment = await initializeTestEnvironment({
    projectId,
    firestore: {
      rules: fs.readFileSync(
        path.resolve(directory, "../firestore.rules"),
        "utf8"),
    },
  });
});

beforeEach(async () => {
  await environment.clearFirestore();
});

after(async () => {
  await environment.cleanup();
});

test("owner cannot create or delete but can read and advance their UID document", async () => {
  const database = environment.authenticatedContext("captain-a").firestore();
  const reference = doc(database, "users/captain-a");

  await assertFails(setDoc(reference, validDocument(1)));
  await environment.withSecurityRulesDisabled(async (context) => {
    await setDoc(
      doc(context.firestore(), "users/captain-a"),
      validDocument(1));
  });
  const created = await assertSucceeds(getDoc(reference));
  assert.equal(created.data().revision, 1);

  const createdAt = created.data().createdAt;
  await assertSucceeds(setDoc(reference, validDocument(2, createdAt)));
  await assertFails(deleteDoc(reference));
});

test("cross-user, unauthenticated, and collection-list access is denied", async () => {
  const owner = environment.authenticatedContext("captain-a").firestore();
  await environment.withSecurityRulesDisabled(async (context) => {
    await setDoc(
      doc(context.firestore(), "users/captain-a"),
      validDocument());
  });

  const other = environment.authenticatedContext("captain-b").firestore();
  const guest = environment.unauthenticatedContext().firestore();
  await assertFails(getDoc(doc(other, "users/captain-a")));
  await assertFails(setDoc(doc(other, "users/captain-a"), validDocument(2)));
  await assertFails(getDoc(doc(guest, "users/captain-a")));
  await assertFails(getDocs(collection(owner, "users")));
});

test("schema rejects photos, settings, malformed missions, and oversized later list entries", async () => {
  const database = environment.authenticatedContext("captain-a").firestore();
  const reference = doc(database, "users/captain-a");
  await environment.withSecurityRulesDisabled(async (context) => {
    await setDoc(
      doc(context.firestore(), "users/captain-a"),
      validDocument(1));
  });
  const createdAt = (await getDoc(reference)).data().createdAt;

  for (const mutate of [
    data => { data.save.photographs = []; },
    data => { data.save.settings = { quality: "high" }; },
    data => { data.save.mission.nodeId = "wrong-field"; },
    data => { data.save.discoveryIds.push("x".repeat(129)); },
    data => { data.birthdayGiftYears = {}; },
  ]) {
    const data = validDocument(2, createdAt);
    mutate(data);
    await assertFails(setDoc(reference, data));
  }
});

test("updates cannot rewrite creation time, reuse a revision, or write outside users", async () => {
  const database = environment.authenticatedContext("captain-a").firestore();
  const reference = doc(database, "users/captain-a");
  await environment.withSecurityRulesDisabled(async (context) => {
    await setDoc(
      doc(context.firestore(), "users/captain-a"),
      validDocument(2));
  });
  const createdAt = (await getDoc(reference)).data().createdAt;

  await assertFails(setDoc(reference, validDocument(2, createdAt)));
  await assertFails(setDoc(reference, validDocument(3)));
  await assertFails(setDoc(
    doc(database, "profiles/captain-a"),
    validDocument(3, createdAt)));
  const forgedGift = validDocument(3, createdAt);
  forgedGift.birthdayGiftYears = [2026];
  await assertFails(setDoc(reference, forgedGift));
});

test("server gift revision blocks stale overwrite and permits a merged successor", async () => {
  const database = environment.authenticatedContext("captain-a").firestore();
  const reference = doc(database, "users/captain-a");
  await environment.withSecurityRulesDisabled(async (context) => {
    await setDoc(
      doc(context.firestore(), "users/captain-a"),
      validDocument(2));
  });
  const createdAt = (await getDoc(reference)).data().createdAt;

  await environment.withSecurityRulesDisabled(async (context) => {
    const serverGift = validDocument(3, createdAt);
    serverGift.clientWriteId = "server-birthday-2026";
    serverGift.birthdayGiftYears = [2026];
    serverGift.save.birthday = {
      hasValue: true,
      day: 4,
      month: 7,
      year: 2013,
      correctionCount: 0,
      lastBirthdayGiftYear: 2026,
    };
    serverGift.save.earnedCosmeticIds = ["birthday.ori-starlight.2026"];
    await setDoc(doc(context.firestore(), "users/captain-a"), serverGift);
  });

  const stale = validDocument(3, createdAt);
  stale.birthdayGiftYears = [2026];
  await assertFails(setDoc(reference, stale));

  const merged = validDocument(4, createdAt);
  merged.birthdayGiftYears = [2026];
  merged.save.birthday = {
    hasValue: true,
    day: 4,
    month: 7,
    year: 2013,
    correctionCount: 0,
    lastBirthdayGiftYear: 2026,
  };
  merged.save.earnedCosmeticIds = ["birthday.ori-starlight.2026"];
  await assertSucceeds(setDoc(reference, merged));
  await assertFails(deleteDoc(reference));

  await environment.withSecurityRulesDisabled(async (context) => {
    await deleteDoc(doc(context.firestore(), "users/captain-a"));
  });
  assert.equal((await getDoc(reference)).exists(), false);
});
