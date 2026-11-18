using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assets.Scripts.Util;
using CableLabels.Contracts;
using UnityEngine;

namespace CableLabels
{
    /// <summary>
    /// Caches custom label meshes per cable prefab, and refits that prefab's labels when a
    /// mesh's change token is cancelled.
    /// </summary>
    /// <remarks>
    /// Everything here runs on the main thread. A token may be cancelled from any thread (SBR
    /// Electrical's solvers run on their own), so its callback only queues the invalidation on
    /// the game's <c>UnityMainThreadDispatcher</c>.
    /// </remarks>
    internal static class CableLabelMeshes
    {
        /// <summary>One cable prefab's answer from <see cref="ICableLabelMount.TryCreateLabelMesh"/>.</summary>
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

        /// <summary>The prefab's entry, asking <paramref name="mount"/> the first time.</summary>
        public static Entry Get(int prefabHash, ICableLabelMount mount)
        {
            if (Entries.TryGetValue(prefabHash, out var entry))
                return entry;
            entry = new Entry { PrefabHash = prefabHash };
            entry.HasMesh = mount.TryCreateLabelMesh(out entry.Mesh, out entry.Offset,
                out entry.Rotation, out var changed) && entry.Mesh != null;
            Entries[prefabHash] = entry;
            // A token already cancelled calls back at once, which queues an invalidation as usual.
            if (changed.CanBeCanceled)
                entry.Registration = changed.Register(() => QueueInvalidate(entry));
            return entry;
        }

        /// <summary>
        /// Records which cable prefab a label is fitted to, so an invalidation finds it;
        /// <paramref name="prefabHash"/> 0 removes it.
        /// </summary>
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

        /// <summary>Drops <paramref name="entry"/> and refits its prefab's labels.</summary>
        private static void Invalidate(Entry entry)
        {
            // A newer entry may have replaced this one already.
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
