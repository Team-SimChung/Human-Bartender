using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    [Serializable]
    public sealed class StudioCatalogEntry
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Id")]
        private string id;
        public string Id
        {
            get { return id; }

            internal set { id = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("ResourceKey")]
        private string resourceKey;
        public string ResourceKey
        {
            get { return resourceKey; }

            internal set { resourceKey = value; }
        }
    }

    public sealed class StudioCatalog : ScriptableObject
    {
        public const string Address = "cutscene-studio/catalog";
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Sequences")]
        private List<StudioCatalogEntry> sequences = new();
        [NonSerialized] private List<StudioCatalogEntry> entryViewSource;
        [NonSerialized] private ReadOnlyCollection<StudioCatalogEntry> entryView;
        public IReadOnlyList<StudioCatalogEntry> Entries
        {
            get
            {
                if (!ReferenceEquals(entryViewSource, sequences) || entryView == null)
                {
                    entryViewSource = sequences;
                    entryView = sequences.AsReadOnly();
                }

                return entryView;
            }
        }

        internal List<StudioCatalogEntry> Sequences
        {
            get { return sequences; }
        }

        public StudioCatalogEntry Find(string id)
        {
            StudioCatalogEntry result = null;
            foreach (var sequence in Sequences)
                if (sequence != null && sequence.Id == id)
                {
                    if (result != null)
                        throw new InvalidOperationException("Duplicate Cutscene Studio ID: " + id);
                    result = sequence;
                }

            return result;
        }
    }
}
