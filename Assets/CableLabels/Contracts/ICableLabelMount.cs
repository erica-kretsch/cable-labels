using System.Threading;
using UnityEngine;

namespace CableLabels.Contracts
{
    /// <summary>
    /// Tells Cable Labels how to fit its labels to a cable. Implement it on the cable class, or on
    /// another component on the cable's GameObject.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Copy this file into your mod verbatim and keep it public. Cable Labels finds your copy by
    /// its full name and binds to its members by name and signature, so your mod needn't
    /// reference Cable Labels.
    /// </para>
    /// <para>
    /// The members never change; a later version would be a new interface beside this one.
    /// </para>
    /// </remarks>
    public interface ICableLabelMount
    {
        /// <summary>
        /// The radius, in metres, at which the label sits on this cable; <c>null</c> to measure
        /// the cable's meshes.
        /// </summary>
        /// <remarks>
        /// Read each time a label refits. Ignored when <see cref="TryCreateLabelMesh"/> returns
        /// <c>true</c>.
        /// </remarks>
        float? CableLabelRadius { get; }

        /// <summary>
        /// A label mesh of your own, for a cable that isn't round.
        /// </summary>
        /// <param name="mesh">The label's mesh: one submesh, drawn in the label's material with
        /// its text texture.</param>
        /// <param name="offset">The mesh's offset in the cable's local frame. Any part along the
        /// cable's run is ignored: the mesh is placed at the centre of the label's cell.</param>
        /// <param name="rotation">The mesh's rotation in the cable's local frame.</param>
        /// <param name="changed">Cancel to make Cable Labels ask again and refit every label on
        /// this cable prefab; <see cref="CancellationToken.None"/> if the mesh never
        /// changes.</param>
        /// <returns><c>true</c> to use <paramref name="mesh"/>; <c>false</c> for the standard
        /// label.</returns>
        /// <remarks>
        /// <para>
        /// Called on the main thread, at most once per cable prefab until <paramref name="changed"/>
        /// is cancelled. The result is kept, so build the mesh here rather than caching it.
        /// <paramref name="changed"/> may be cancelled from any thread.
        /// </para>
        /// <para>
        /// The pose is in the cable's frame because a label follows the cable's run axis but not
        /// its roll, which matters for a cable that isn't rotationally symmetric of order 4. The
        /// mesh may be any shape and length.
        /// </para>
        /// <para>
        /// Lay out the UVs as the standard label's: each face that shows text maps the whole
        /// 512 × 128 text texture, v up and u left to right as read from outside.
        /// </para>
        /// </remarks>
        bool TryCreateLabelMesh(out Mesh mesh, out Vector3 offset, out Quaternion rotation,
            out CancellationToken changed);
    }
}
