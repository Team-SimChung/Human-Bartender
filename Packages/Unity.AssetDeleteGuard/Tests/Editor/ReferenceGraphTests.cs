using System;
using NUnit.Framework;

namespace AssetDeleteGuard.Tests
{
    public sealed class ReferenceGraphTests
    {
        [Test]
        public void ReplacingSourceRemovesObsoleteReverseEdges()
        {
            var graph = new ReferenceGraph();
            graph.Replace("prefab", new[] { "material" });
            graph.Replace("prefab", new[] { "texture" });
            Assert.That(graph.GetReferencers("material"), Is.Empty);
            Assert.That(graph.GetReferencers("texture"), Is.EqualTo(new[] { "prefab" }));
        }

        [Test]
        public void CyclesTerminateAndDirectReferencersAreNotIndirect()
        {
            var graph = new ReferenceGraph();
            graph.Replace("scene", new[] { "prefab" });
            graph.Replace("prefab", new[] { "material" });
            graph.Replace("material", new[] { "prefab" });
            Assert.That(graph.GetReferencers("material"), Is.EqualTo(new[] { "prefab" }));
            Assert.That(graph.GetIndirectReferencers(new[] { "material" }), Is.EqualTo(new[] { "scene" }));
        }

        [Test]
        public void RemoveOnlyRemovesThatSourcesOutgoingEdges()
        {
            var graph = new ReferenceGraph();
            graph.Replace("A", new[] { "B", "B", "A" });
            graph.Replace("C", new[] { "B" });
            graph.Remove("A");
            Assert.That(graph.GetReferencers("B"), Is.EqualTo(new[] { "C" }));
        }

        [Test]
        public void RootReductionUsesPathSegments()
        {
            Assert.That(AssetPaths.ReduceRoots(new[] { "Assets/A/child.asset", "Assets/AB/other.asset", "Assets/A", "Assets/A/" }),
                Is.EqualTo(new[] { "Assets/A", "Assets/AB/other.asset" }));
        }

        [TestCase("Assets")]
        [TestCase("Packages/test/asset")]
        [TestCase("Assets/../outside")]
        [TestCase("Assets/target.meta")]
        [TestCase("Assets//asset")]
        [TestCase("C:/project/Assets/asset")]
        public void UnsafeRootIsRejected(string path)
        {
            Assert.Throws<ArgumentException>(() => AssetPaths.ReduceRoots(new[] { path }));
        }
    }
}
