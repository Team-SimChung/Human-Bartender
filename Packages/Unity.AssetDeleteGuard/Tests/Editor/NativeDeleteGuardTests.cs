using System;
using NUnit.Framework;

namespace AssetDeleteGuard.Tests
{
    public sealed class NativeDeleteGuardTests
    {
        [Test]
        public void ProviderLocalSourceIdsCannotMergeDifferentReferenceSources()
        {
            var first = new ReferenceEntry { ProviderId = "first", SourceId = "settings" };
            var second = new ReferenceEntry { ProviderId = "second", SourceId = "settings" };
            var repeated = new ReferenceEntry { ProviderId = "first", SourceId = "settings", TargetPath = "Assets/other.asset" };
            Assert.That(first.SourceKey, Is.Not.EqualTo(second.SourceKey));
            Assert.That(first.SourceKey, Is.EqualTo(repeated.SourceKey));
            first.ProviderId = "a"; first.SourceId = "bc";
            second.ProviderId = "ab"; second.SourceId = "c";
            Assert.That(first.SourceKey, Is.Not.EqualTo(second.SourceKey));
        }

        [TestCase("Assets/item.asset", true, true, true)]
        [TestCase("Assets/folder", true, true, true)]
        [TestCase("Assets/item.asset", false, true, false)]
        [TestCase("Assets/item.asset", true, false, false)]
        [TestCase("Assets", true, true, false)]
        [TestCase("Packages/package/item.asset", true, true, false)]
        [TestCase("Assets/../outside", true, true, false)]
        [TestCase("Assets/item.asset.meta", true, true, false)]
        public void NativeGuardHasAnExplicitInteractiveProjectAssetBoundary(string path, bool interactive, bool enabled, bool blocked)
        {
            Assert.That(NativeDeleteBridge.ShouldBlock(path, interactive, enabled), Is.EqualTo(blocked));
        }

        [Test]
        public void ApprovalAuthorizesOnlyTheExactReviewedInventory()
        {
            using (ApprovedDeletionScope.Begin(new[] { "Assets/folder", "Assets/folder/a.asset", "Assets/folder/a.asset.meta" }))
            {
                Assert.That(NativeDeleteBridge.ShouldBlock("Assets/folder/a.asset", true, true), Is.False);
                Assert.That(ApprovedDeletionScope.Contains("Assets/folder/a.asset.meta"), Is.True);
                Assert.That(NativeDeleteBridge.ShouldBlock("Assets/folder/new.asset", true, true), Is.True);
                Assert.That(NativeDeleteBridge.ShouldBlock("Assets/other.asset", true, true), Is.True);
                Assert.Throws<InvalidOperationException>(() => ApprovedDeletionScope.Begin(new[] { "Assets/other.asset" }));
            }
            Assert.That(NativeDeleteBridge.ShouldBlock("Assets/folder/a.asset", true, true), Is.True);
        }

        [Test]
        public void ThrowingBackendCannotLeakAnApprovalScope()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (ApprovedDeletionScope.Begin(new[] { "Assets/a.asset" })) throw new InvalidOperationException("backend failure");
            });
            Assert.That(ApprovedDeletionScope.Contains("Assets/a.asset"), Is.False);
            using (ApprovedDeletionScope.Begin(new[] { "Assets/b.asset" })) Assert.That(ApprovedDeletionScope.Contains("Assets/b.asset"), Is.True);
        }

        [Test]
        public void DisposedControllerCannotRetainOrAcquireApproval()
        {
            var controller = new DeleteReviewController(new[] { "Assets/a.asset" }, true);
            controller.Dispose();
            controller.Acknowledge(true);
            controller.Scan();
            Assert.That(controller.CanExecute, Is.False);
            Assert.That(controller.Acknowledged, Is.False);
            Assert.That(controller.Report, Is.Null);
            Assert.That(controller.IsScanning, Is.False);
        }
    }
}
