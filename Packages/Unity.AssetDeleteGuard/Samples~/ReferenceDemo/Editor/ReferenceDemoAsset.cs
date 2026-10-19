using UnityEngine;

namespace AssetDeleteGuard.Samples
{
    /// <summary>Editor-only data for the reference inspection demonstration.</summary>
    public sealed class ReferenceDemoAsset : ScriptableObject
    {
        public Object referencedAsset;
        [TextArea] public string explanation;
    }
}
