using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AssetDeleteGuard.Tests
{
    // Destructive integration tests only run in a project explicitly created by Tools~/run-tests.ps1.
    public sealed class DeletionIntegrationTests
    {
        private string folder;
        private EditorBuildSettingsScene[] originalScenes;
        private UnityEngine.Object[] originalPreloaded;
        private Scene testScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (!File.Exists(Path.Combine(ProjectFiles.Root, ".asset-delete-guard-fixture")))
                Assert.Ignore("Run in the disposable project created by Tools~/run-tests.ps1.");
            folder = "Assets/__DeleteGuardTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            originalScenes = EditorBuildSettings.scenes;
            originalPreloaded = PlayerSettings.GetPreloadedAssets();
            var baseline = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(baseline, folder + "/baseline.unity");
            testScene = default(Scene);
            yield return WaitForIdle();
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(folder)) return;
            ReferenceProviders.Unregister("test-provider");
            EditorBuildSettings.scenes = originalScenes;
            PlayerSettings.SetPreloadedAssets(originalPreloaded);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }

        private static IEnumerator WaitForIdle()
        {
            // Allow imports and delayed projectChanged callbacks to settle before taking a snapshot.
            yield return null;
            yield return null;
            var deadline = EditorApplication.timeSinceStartup + 30;
            while (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Editor did not become idle.");
                yield return null;
            }
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False, "Requires Edit Mode.");
        }

        private string Asset(string name, UnityEngine.Object reference = null)
        {
            var path = folder + "/" + name + ".asset";
            var obj = ScriptableObject.CreateInstance<GuardTestAsset>();
            obj.reference = reference;
            AssetDatabase.CreateAsset(obj, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static void AssertReady(AnalysisReport report)
        {
            Assert.That(report.CanDelete, Is.True, string.Join("\n", report.Errors.ToArray()));
        }

        [UnityTest]
        public IEnumerator SavedDirectIndirectAndInternalReferencesAreSeparated()
        {
            var target = Asset("target");
            var middle = Asset("middle", AssetDatabase.LoadMainAssetAtPath(target));
            var outer = Asset("outer", AssetDatabase.LoadMainAssetAtPath(middle));
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            AssertReady(report);
            Assert.That(report.References.Any(r => r.SourcePath == middle && r.TargetPath == target), Is.True);
            Assert.That(report.IndirectReferencers, Does.Contain(outer));
            report = ReferenceAnalyzer.Analyze(new[] { target, middle });
            AssertReady(report);
            Assert.That(report.References.Any(r => r.SourcePath == middle), Is.False);
            Assert.That(report.References.Any(r => r.SourcePath == outer && r.TargetPath == middle), Is.True);
        }

        [UnityTest]
        public IEnumerator FolderInventoryIncludesNonImportedFilesAndMetadata()
        {
            Asset("target");
            File.WriteAllText(ProjectFiles.Absolute(folder + "/.hidden"), "unimported file");
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { folder });
            AssertReady(report);
            Assert.That(report.DiskPaths, Does.Contain(folder + "/.hidden"));
            Assert.That(report.DiskPaths, Does.Contain(folder + ".meta"));
            Assert.That(report.Warnings.Any(w => w.Contains("Not indexed")), Is.True);
        }

        [UnityTest]
        public IEnumerator UnsavedInactiveSceneObjectReferenceIsReported()
        {
            var target = folder + "/material.mat";
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            AssetDatabase.CreateAsset(material, target);
            AssetDatabase.SaveAssets();
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var go = new GameObject("unsaved inactive referrer");
            SceneManager.MoveGameObjectToScene(go, testScene);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            AssertReady(report);
            Assert.That(report.References.Any(r => r.Kind == ReferenceKind.OpenObject && r.TargetPath == target), Is.True);
        }

        [UnityTest]
        public IEnumerator UnsavedSceneSkyboxReferenceIsReported()
        {
            var path = folder + "/skybox.mat";
            var material = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            var previousActive = SceneManager.GetActiveScene();
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(testScene);
            try
            {
                RenderSettings.skybox = material;
                yield return WaitForIdle();
                var report = ReferenceAnalyzer.Analyze(new[] { path });
                AssertReady(report);
                Assert.That(report.References.Any(r => r.Kind == ReferenceKind.OpenObject && r.TargetPath == path), Is.True);
            }
            finally { SceneManager.SetActiveScene(previousActive); }
        }

        [UnityTest]
        public IEnumerator InstanceMaterialInUnsavedSceneIsTraversed()
        {
            var target = folder + "/texture.asset";
            var texture = new Texture2D(2, 2);
            AssetDatabase.CreateAsset(texture, target);
            AssetDatabase.SaveAssets();
            var material = new Material(Shader.Find("Unlit/Texture"));
            try
            {
                material.mainTexture = texture;
                testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var go = new GameObject("instance material referrer");
                SceneManager.MoveGameObjectToScene(go, testScene);
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                yield return WaitForIdle();
                var report = ReferenceAnalyzer.Analyze(new[] { target });
                AssertReady(report);
                Assert.That(report.References.Any(r => r.Kind == ReferenceKind.OpenObject && r.TargetPath == target), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [UnityTest]
        public IEnumerator DirtySelectedAssetBlocksDeletion()
        {
            var target = Asset("target");
            var obj = AssetDatabase.LoadMainAssetAtPath(target);
            EditorUtility.SetDirty(obj);
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            Assert.That(report.CanDelete, Is.False);
            Assert.That(report.Errors.Any(e => e.Contains("modified asset")), Is.True);
            AssetDatabase.SaveAssets();
        }

        [UnityTest]
        public IEnumerator DisabledBuildSceneAndPreloadedAssetsAreReported()
        {
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var scenePath = folder + "/test.unity";
            EditorSceneManager.SaveScene(testScene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, false) };
            var target = Asset("preloaded");
            PlayerSettings.SetPreloadedAssets(new[] { AssetDatabase.LoadMainAssetAtPath(target) });
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { scenePath, target });
            AssertReady(report);
            Assert.That(report.References.Any(r => r.SourceLabel.Contains("disabled") && r.TargetPath == scenePath), Is.True);
            Assert.That(report.References.Any(r => r.SourceLabel.Contains("Preloaded") && r.TargetPath == target), Is.True);
        }

        [UnityTest]
        public IEnumerator NewReferencerInvalidatesReviewedDeletion()
        {
            var target = Asset("target");
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            AssertReady(report);
            Asset("new-referrer", AssetDatabase.LoadMainAssetAtPath(target));
            var backend = new RefusingBackend();
            yield return WaitForIdle();
            var result = DeleteExecutor.Execute(report, backend);
            Assert.That(backend.Calls, Is.Zero);
            Assert.That(result.UpdatedReport, Is.Not.Null);
            Assert.That(result.UpdatedReport.Generation, Is.EqualTo(ProjectChanges.Generation),
                "Returning a fresh report must not manufacture another project change.");
            Assert.That(ProjectFiles.Exists(target), Is.True);
        }

        [UnityTest]
        public IEnumerator NewFileInReviewedFolderInvalidatesDeletion()
        {
            Asset("target");
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { folder });
            AssertReady(report);
            File.WriteAllText(ProjectFiles.Absolute(folder + "/.new-hidden"), "new file");
            var backend = new RefusingBackend();
            yield return WaitForIdle();
            DeleteExecutor.Execute(report, backend);
            Assert.That(backend.Calls, Is.Zero);
            Assert.That(ProjectFiles.Exists(folder), Is.True);
        }

        [UnityTest]
        public IEnumerator ReusedPathWithNewGuidInvalidatesDeletion()
        {
            var target = Asset("target");
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            AssetDatabase.DeleteAsset(target);
            Asset("target");
            var backend = new RefusingBackend();
            yield return WaitForIdle();
            DeleteExecutor.Execute(report, backend);
            Assert.That(backend.Calls, Is.Zero);
            Assert.That(ProjectFiles.Exists(target), Is.True);
        }

        [UnityTest]
        public IEnumerator ProviderFailureCannotBecomeReady()
        {
            var target = Asset("target");
            ReferenceProviders.Register(new FailingProvider());
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            Assert.That(report.CanDelete, Is.False);
            Assert.That(report.Errors.Any(e => e.Contains("test-provider")), Is.True);
        }

        [UnityTest]
        public IEnumerator CancellationCannotProduceUsableApproval()
        {
            var session = new AnalysisSession(new[] { Asset("target") });
            yield return WaitForIdle();
            session.Step();
            session.Dispose();
            Assert.That(session.Report.State, Is.EqualTo(AnalysisState.Cancelled));
            Assert.That(session.Report.CanDelete, Is.False);
        }

        [UnityTest]
        public IEnumerator SuccessfulTrashDeletionRemovesAssetAndMetaAndConsumesApproval()
        {
            var target = Asset("target");
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            AssertReady(report);
            yield return WaitForIdle();
            var result = DeleteExecutor.Execute(report, new UnityTrashBackend());
            Assert.That(result.Error, Is.Null.Or.Empty, result.Error);
            Assert.That(result.Items.Single().Status, Is.EqualTo("Deleted"));
            Assert.That(ProjectFiles.Exists(target), Is.False);
            Assert.That(File.Exists(ProjectFiles.Absolute(target) + ".meta"), Is.False);
            var retry = new RefusingBackend();
            DeleteExecutor.Execute(report, retry);
            Assert.That(retry.Calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FailureIsNotRetriedOrChangedToPermanentDeletion()
        {
            var target = Asset("target");
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            var backend = new RefusingBackend();
            yield return WaitForIdle();
            var result = DeleteExecutor.Execute(report, backend);
            Assert.That(backend.Calls, Is.EqualTo(1));
            Assert.That(ProjectFiles.Exists(target), Is.True);
            Assert.That(result.Items.Single().Status, Is.EqualTo("Failed or partially deleted"));
            Assert.That(result.Error, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator PartialFailureReportsEachRootWithoutRetry()
        {
            var first = Asset("a");
            var second = Asset("b");
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { first, second });
            yield return WaitForIdle();
            var result = DeleteExecutor.Execute(report, new PartialBackend());
            Assert.That(result.Items.Single(i => i.Path == first).Status, Is.EqualTo("Deleted"));
            Assert.That(result.Items.Single(i => i.Path == second).Status, Is.EqualTo("Failed or partially deleted"));
            Assert.That(ProjectFiles.Exists(second), Is.True);
        }

        [UnityTest]
        public IEnumerator ReviewControllerRescanAndDisposalRevokeApproval()
        {
            var target = Asset("controller-target");
            yield return WaitForIdle();
            using (var controller = new DeleteReviewController(new[] { target }, true))
            {
                controller.Scan();
                while (controller.IsScanning) controller.Advance();
                AssertReady(controller.Report);
                controller.Acknowledge(true);
                Assert.That(controller.CanExecute, Is.True);
                controller.Scan();
                Assert.That(controller.CanExecute, Is.False);
                Assert.That(controller.Acknowledged, Is.False);
                controller.CancelScan();
                Assert.That(controller.Report.State, Is.EqualTo(AnalysisState.Cancelled));
                controller.Acknowledge(true);
                Assert.That(controller.CanExecute, Is.False);
                controller.Reset();
                Assert.That(controller.Report, Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator ExecutorPermitIsScopedToReviewedFilesAndReleasedAfterException()
        {
            var target = Asset("permitted");
            var other = Asset("not-permitted");
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            AssertReady(report);
            var backend = new InspectingThrowingBackend(other);
            var result = DeleteExecutor.Execute(report, backend);
            Assert.That(backend.Called, Is.True);
            Assert.That(result.Error, Does.Contain("fixture exception"));
            Assert.That(ApprovedDeletionScope.Contains(target), Is.False);
            Assert.That(report.Consumed, Is.True);
            Assert.That(ProjectFiles.Exists(target), Is.True);
            Assert.That(ProjectFiles.Exists(other), Is.True);
        }

        [UnityTest]
        public IEnumerator MoreThanTenReferencersRemainWhenTheReviewedTargetIsReallyDeleted()
        {
            var target = Asset("many-target");
            var targetObject = AssetDatabase.LoadMainAssetAtPath(target);
            var sources = new List<string>();
            for (var i = 0; i < 13; i++) sources.Add(Asset("source-" + i.ToString("00"), targetObject));
            var indirect = Asset("indirect", AssetDatabase.LoadMainAssetAtPath(sources[0]));
            yield return WaitForIdle();
            var report = ReferenceAnalyzer.Analyze(new[] { target });
            AssertReady(report);
            Assert.That(report.References.Count, Is.EqualTo(13));
            Assert.That(report.References.Select(r => r.SourceKey).Distinct().Count(), Is.EqualTo(13));
            Assert.That(report.References.Select(r => r.SourcePath), Is.EquivalentTo(sources));
            Assert.That(report.IndirectReferencers, Does.Contain(indirect));
            var sourceContents = sources.ToDictionary(p => p, p => File.ReadAllBytes(ProjectFiles.Absolute(p)));
            var result = DeleteExecutor.Execute(report, new UnityTrashBackend());
            Assert.That(result.Error, Is.Null.Or.Empty, result.Error);
            Assert.That(result.Items.Single().Status, Is.EqualTo("Deleted"));
            Assert.That(ProjectFiles.Exists(target), Is.False);
            Assert.That(File.Exists(ProjectFiles.Absolute(target) + ".meta"), Is.False);
            Assert.That(report.Consumed, Is.True);
            foreach (var source in sources)
            {
                Assert.That(File.Exists(ProjectFiles.Absolute(source)), Is.True, source);
                Assert.That(File.Exists(ProjectFiles.Absolute(source) + ".meta"), Is.True, source + ".meta");
                Assert.That(File.ReadAllBytes(ProjectFiles.Absolute(source)), Is.EqualTo(sourceContents[source]),
                    "Deleting the target must not rewrite or delete its referencers: " + source);
            }
            Assert.That(ProjectFiles.Exists(indirect), Is.True);
            Assert.That(ApprovedDeletionScope.Contains(target), Is.False);
            var retry = new RefusingBackend();
            DeleteExecutor.Execute(report, retry);
            Assert.That(retry.Calls, Is.Zero);
        }

        private sealed class InspectingThrowingBackend : ITrashBackend
        {
            private readonly string other;
            internal bool Called;
            internal InspectingThrowingBackend(string unrelated) { other = unrelated; }
            public bool Move(string[] roots, List<string> failed)
            {
                Called = true;
                Assert.That(NativeDeleteBridge.ShouldBlock(roots[0], true, true), Is.False);
                Assert.That(NativeDeleteBridge.ShouldBlock(other, true, true), Is.True);
                throw new IOException("fixture exception");
            }
        }

        private sealed class RefusingBackend : ITrashBackend
        {
            internal int Calls;
            public bool Move(string[] roots, List<string> failed) { Calls++; failed.AddRange(roots); return false; }
        }
        private sealed class PartialBackend : ITrashBackend
        {
            public bool Move(string[] roots, List<string> failed)
            {
                AssetDatabase.MoveAssetToTrash(roots[0]);
                failed.Add(roots[1]);
                return false;
            }
        }
        private sealed class FailingProvider : IReferenceProvider
        {
            public string Id { get { return "test-provider"; } }
            public string Version { get { return "1"; } }
            public ProviderResult Collect(ReferenceQuery query) { throw new IOException("test failure"); }
        }
    }
}
