using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.Animation2D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class CrewSpriteMotionTests
    {
        private static readonly string[] Ids = { "mira", "juno", "kai", "bea", "ori" };
        private readonly List<GameObject> objects = new();
        private readonly List<AsyncOperationHandle<CharacterSpriteSet>> handles = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var target in objects)
            {
                UnityEngine.Object.Destroy(target);
            }
            objects.Clear();
            yield return null;
            foreach (var handle in handles)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            handles.Clear();
        }

        [UnityTest]
        public IEnumerator CrewAndOri_PlayEveryRealClipOnOneRendererWithLiveAnchors()
        {
            foreach (var id in Ids)
            {
                var handle = Addressables.LoadAssetAsync<CharacterSpriteSet>(
                    $"Characters/Crew/{id}");
                handles.Add(handle);
                yield return handle;
                Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded), id);
                var spriteSet = handle.Result;
                Assert.That(spriteSet, Is.Not.Null, id);

                var target = new GameObject($"CrewSpriteMotionTests-{id}");
                objects.Add(target);
                var renderer = target.AddComponent<SpriteRenderer>();
                var animator = target.AddComponent<SpriteAtlasAnimator>();
                animator.Configure(renderer);
                Assert.That(target.GetComponentsInChildren<SpriteRenderer>(), Has.Length.EqualTo(1));
                Assert.That(target.GetComponent("LayeredCharacterRenderer"), Is.Null);

                foreach (var clip in spriteSet.Clips)
                {
                    var observed = new List<string>();
                    animator.FrameEventEmitted += OnEvent;
                    animator.Play(clip);
                    Assert.That(renderer.sprite, Is.SameAs(clip.Frames[0]));
                    AssertAnchorResolution(spriteSet, clip.StableId, 0);
                    var prior = renderer.sprite;
                    for (var frameIndex = 1; frameIndex < clip.Frames.Count; frameIndex++)
                    {
                        animator.Advance(clip.FrameDurations[frameIndex - 1] + 0.00001f);
                        Assert.That(renderer.sprite, Is.SameAs(clip.Frames[frameIndex]));
                        Assert.That(renderer.sprite, Is.Not.SameAs(prior));
                        AssertAnchorResolution(spriteSet, clip.StableId, frameIndex);
                        prior = renderer.sprite;
                    }
                    animator.FrameEventEmitted -= OnEvent;
                    Assert.That(
                        observed,
                        Is.EqualTo(clip.FrameEvents.Select(frameEvent => frameEvent.Id)));

                    void OnEvent(SpriteFrameEvent frameEvent) => observed.Add(frameEvent.Id);
                }
            }
        }

        private static void AssertAnchorResolution(
            CharacterSpriteSet spriteSet,
            string clipId,
            int frameIndex)
        {
            var method = typeof(CharacterSpriteSet).GetMethod(
                "ResolveFrameAnchors",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null,
                "CharacterSpriteSet must expose live per-frame anchors.");
            var result = method.Invoke(spriteSet, new object[] { clipId, frameIndex }) as IEnumerable;
            Assert.That(result, Is.Not.Null);
            var anchors = result.Cast<SpriteFrameAnchor>().ToArray();
            Assert.That(anchors, Has.Length.EqualTo(14));
            if (clipId.Contains(".scan.", StringComparison.Ordinal))
            {
                Assert.That(
                    anchors.Single(anchor => anchor.Id == "ActiveTool")
                        .IsAuthoredVisible,
                    Is.True);
            }
        }
    }
}
