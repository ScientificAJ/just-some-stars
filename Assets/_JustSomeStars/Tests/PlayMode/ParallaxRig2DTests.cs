using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class ParallaxRig2DTests
    {
        [Test]
        public void Parallax_UsesDeclaredFactorWithoutDrift()
        {
            var camera = new GameObject("CameraAnchor");
            var layer = new GameObject("FarWorld");
            var rig = new GameObject("ParallaxRig");
            try
            {
                camera.transform.position = new Vector3(2f, 1f, 0f);
                layer.transform.position = new Vector3(5f, -2f, 0f);
                var layerComponent = Stage1RuntimeReflection.AddComponent(
                    layer,
                    "JustSomeStars.Runtime.Rendering2D.ParallaxLayer2D");
                Stage1RuntimeReflection.Invoke(
                    layerComponent,
                    "Configure",
                    0.35f,
                    Vector2.one);

                var rigComponent = Stage1RuntimeReflection.AddComponent(
                    rig,
                    "JustSomeStars.Runtime.Rendering2D.ParallaxRig2D");
                var layerArray = System.Array.CreateInstance(
                    layerComponent.GetType(),
                    1);
                layerArray.SetValue(layerComponent, 0);
                Stage1RuntimeReflection.Invoke(
                    rigComponent,
                    "Configure",
                    camera.transform,
                    layerArray);
                Stage1RuntimeReflection.Invoke(rigComponent, "CaptureOrigins");

                camera.transform.position += new Vector3(10f, 4f, 0f);
                Stage1RuntimeReflection.Invoke(rigComponent, "ApplyNow");
                Assert.That(layer.transform.position.x,
                    Is.EqualTo(8.5f).Within(0.001f));
                Assert.That(layer.transform.position.y,
                    Is.EqualTo(-0.6f).Within(0.001f));

                Stage1RuntimeReflection.Invoke(rigComponent, "ApplyNow");
                Assert.That(layer.transform.position.x,
                    Is.EqualTo(8.5f).Within(0.001f));
                Assert.That(layer.transform.position.y,
                    Is.EqualTo(-0.6f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(layer);
                Object.DestroyImmediate(camera);
            }
        }

        [Test]
        public void Parallax_RespectsIndependentAxisScaleAndMotionReduction()
        {
            var camera = new GameObject("CameraAnchor");
            var layer = new GameObject("Atmosphere");
            var rig = new GameObject("ParallaxRig");
            try
            {
                var layerComponent = Stage1RuntimeReflection.AddComponent(
                    layer,
                    "JustSomeStars.Runtime.Rendering2D.ParallaxLayer2D");
                Stage1RuntimeReflection.Invoke(
                    layerComponent,
                    "Configure",
                    0.5f,
                    new Vector2(1f, 0.25f));
                var rigComponent = Stage1RuntimeReflection.AddComponent(
                    rig,
                    "JustSomeStars.Runtime.Rendering2D.ParallaxRig2D");
                var layerArray = System.Array.CreateInstance(
                    layerComponent.GetType(),
                    1);
                layerArray.SetValue(layerComponent, 0);
                Stage1RuntimeReflection.Invoke(
                    rigComponent,
                    "Configure",
                    camera.transform,
                    layerArray);
                Stage1RuntimeReflection.Invoke(rigComponent, "CaptureOrigins");
                Stage1RuntimeReflection.Invoke(rigComponent, "SetMotionScale", 0.25f);

                camera.transform.position = new Vector3(8f, 8f, 0f);
                Stage1RuntimeReflection.Invoke(rigComponent, "ApplyNow");

                Assert.That(layer.transform.position.x,
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(layer.transform.position.y,
                    Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(
                    Stage1RuntimeReflection.Read<float>(
                        rigComponent,
                        "MotionScale"),
                    Is.EqualTo(0.25f));
            }
            finally
            {
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(layer);
                Object.DestroyImmediate(camera);
            }
        }
    }
}
