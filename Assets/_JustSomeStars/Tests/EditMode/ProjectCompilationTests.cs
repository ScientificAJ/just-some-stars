using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class ProjectCompilationTests
    {
        [Test]
        public void ProjectAssembly_IsLoadable() =>
            Assert.That(typeof(JustSomeStars.Runtime.ProjectMarker), Is.Not.Null);
    }
}
