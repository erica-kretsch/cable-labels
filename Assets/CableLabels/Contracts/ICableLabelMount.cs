using System.Threading;
using UnityEngine;

// The contract another mod implements to fit cable labels to its own cables, without
// referencing this mod. Copy this file into that mod VERBATIM: same namespace, same name, same
// members. Cable Labels finds the copy by its full name, not by type identity (a copy compiled
// into another assembly is a different type to .NET), and binds to it by member name and
// signature (CableLabelMounts).
//
// Implement it on the cable class itself, or on any other component on the cable's GameObject.
// Keep the copy public.
//
// Never change the members: a change would break every existing copy. A later version would be
// a new interface beside it (ICableLabelMount2).
namespace CableLabels.Contracts
{
    public interface ICableLabelMount
    {
        // The radius, in metres, the label's panels should sit at on this cable. Null to let
        // the label measure the cable's meshes, which suits any round cable. Read again every
        // time a label refits.
        float? CableLabelRadius { get; }

        // For a cable that isn't round: the mesh the label should use on it, and that mesh's
        // offset and rotation in the cable's cross-section (the label's local space; z runs
        // along the cable, and an offset along z is ignored, since a label sits in its own cell
        // of a long cable). Return false to use the standard label.
        //
        // Called at most once per cable prefab: Cable Labels keeps the result, false included,
        // so build the mesh here and don't cache it yourself.
        //
        // `changed` says when that result goes stale: cancel it (CancellationTokenSource.Cancel,
        // from any thread) and Cable Labels drops the result, calls this again for a new one and
        // a new token, and refits every label on that prefab, rereading CableLabelRadius too.
        // CancellationToken.None if it never changes.
        //
        // The mesh has one submesh, drawn in the label's material with the label's text texture.
        // Lay out its UVs as the standard label's: each panel maps the whole text texture, v up
        // and u left to right as read from outside.
        bool TryCreateLabelMesh(out Mesh mesh, out Vector3 offset, out Quaternion rotation,
            out CancellationToken changed);
    }
}
