using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assets.Scripts.Util;
using CableLabels.Contracts;
using UnityEngine;

namespace CableLabels
{
    // The custom label meshes other mods' cables give (ICableLabelMount.TryCreateLabelMesh),
    // one per cable prefab, and the labels fitted on each prefab, so a mesh going stale refits
    // them.
    //
    // Everything here runs on the main thread. A mount may cancel its token from any thread
    // (SBR has solver threads), so the token's callback only queues the invalidation on the
    // game's UnityMainThreadDispatcher, whose queue is locked.
    internal static class CableLabelMeshes
    {
        internal sealed class Entry
        {
            public int PrefabHash;
            public bool HasMesh;
            public Mesh Mesh;
            public Vector3 Offset;
            public Quaternion Rotation;
            public CancellationTokenRegistration Registration;
        }

        private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
        private static readonly Dictionary<int, HashSet<CableLabel>> Labels =
            new Dictionary<int, HashSet<CableLabel>>();

        // The prefab's entry, asking the mount for it the first time.
        public static Entry Get(int prefabHash, ICableLabelMount mount)
        {
            if (Entries.TryGetValue(prefabHash, out var entry))
                return entry;
            entry = new Entry { PrefabHash = prefabHash };
            entry.HasMesh = mount.TryCreateLabelMesh(out entry.Mesh, out entry.Offset,
                out entry.Rotation, out var changed) && entry.Mesh != null;
            Entries[prefabHash] = entry;
            // A token already cancelled runs this at once, which queues an invalidation like any
            // other.
            if (changed.CanBeCanceled)
                entry.Registration = changed.Register(() => QueueInvalidate(entry));
            return entry;
        }

        // Which prefab a label is fitted on, so an invalidation finds it. A label moves when the
        // cable under it changes, and leaves when it's destroyed (prefabHash 0).
        public static void Track(CableLabel label, int previousHash, int prefabHash)
        {
            if (previousHash == prefabHash)
                return;
            if (previousHash != 0 && Labels.TryGetValue(previousHash, out var old))
                old.Remove(label);
            if (prefabHash == 0)
                return;
            if (!Labels.TryGetValue(prefabHash, out var set))
                Labels[prefabHash] = set = new HashSet<CableLabel>();
            set.Add(label);
        }

        private static void QueueInvalidate(Entry entry)
        {
            if (UnityMainThreadDispatcher.Exists())
                UnityMainThreadDispatcher.Instance().Enqueue(() => Invalidate(entry));
        }

        private static void Invalidate(Entry entry)
        {
            // A later entry may have replaced this one already; only drop this one.
            if (!Entries.TryGetValue(entry.PrefabHash, out var current) || current != entry)
                return;
            Entries.Remove(entry.PrefabHash);
            entry.Registration.Dispose();
            if (!Labels.TryGetValue(entry.PrefabHash, out var labels))
                return;
            foreach (var label in labels.ToList())
            {
                if (label != null)
                    label.Refit();
            }
        }
    }
}
