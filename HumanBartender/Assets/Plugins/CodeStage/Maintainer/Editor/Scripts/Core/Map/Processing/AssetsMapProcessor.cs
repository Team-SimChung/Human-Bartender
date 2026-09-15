#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.Processing
{
    using System;
    using UnityEngine;
    using Progress;
    using ChangeTracking;

    internal class AssetsMapProcessor
    {
        private readonly IProgressReporter progressReporter;
        private readonly AssetUpdateProcessor updateProcessor;
        private readonly ReferenceProcessor referenceProcessor;

        public AssetsMapProcessor(IProgressReporter progressReporter)
        {
            this.progressReporter = progressReporter;
            updateProcessor = new AssetUpdateProcessor(progressReporter);
            referenceProcessor = new ReferenceProcessor(progressReporter);
        }

        public bool ProcessMap(AssetsMap map)
        {
            try
            {
                // Fast path: skip processing if no changes detected and map is already populated
                // Don't skip on first-ever run (isDirty new map), when map is empty, when baseline is missing, or when change tracker index is empty
                var hasBaseline = AssetsChangeTracker.HasBaseline();
                var hasChanges = AssetsChangeTracker.HasChanges();
                var pathsUnchanged = AssetsChangeTracker.PathsCountUnchanged();
                var indexEmpty = AssetsChangeTracker.IsIndexEmpty();
                var fastPathTaken = !map.isDirty && hasBaseline && !hasChanges && pathsUnchanged && !indexEmpty;
                if (fastPathTaken)
                {
                    // Clear any accumulated changes even when fast path is taken
                    // This ensures subsequent searches can also use the fast path
                    AssetsChangeTracker.TakeSnapshot();
                    return true;
                }

                if (!ProcessExistingAssets(map))
                    return false;

                if (!ProcessNewAssets(map))
                    return false;

                if (!ProcessReferences(map))
                    return false;

                AssetsChangeTracker.TakeSnapshot();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
            finally
            {
                progressReporter.ClearProgress();
            }
        }

        private bool ProcessExistingAssets(AssetsMap map)
        {
            return updateProcessor.ProcessExistingAssets(map);
        }

        private bool ProcessNewAssets(AssetsMap map)
        {
            return updateProcessor.ProcessNewAssets(map);
        }

        private bool ProcessReferences(AssetsMap map)
        {
            return referenceProcessor.ProcessReferences(map);
        }
    }
} 
