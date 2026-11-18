using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Assets.Scripts.Objects.Electrical;
using CableLabels.Contracts;
using UnityEngine;

namespace CableLabels
{
    // Finds another mod's copy of ICableLabelMount on a cable and adapts it to ours.
    //
    // A copy compiled into another assembly is a different type, so `is ICableLabelMount` never
    // matches it. Instead this looks for an implemented interface with our interface's full
    // name, checks each of our members has a counterpart with the same name and signature, and
    // compiles one forwarding delegate per member, once per implementing type. Calls through the
    // adapter then cost about what a direct interface call does. A copy that doesn't match is
    // logged once and ignored.
    //
    // Hand-written for this one contract. A general Implements<T, TResult> would generate the
    // adapter type with Reflection.Emit instead.
    internal static class CableLabelMounts
    {
        private delegate bool TryGetLabelMeshFn(object target, out Mesh mesh, out Vector3 offset,
            out Quaternion rotation);

        // One per implementing type: the compiled members, or null where the type has no
        // matching copy (also cached, so each type is examined once).
        private sealed class Binding
        {
            public Func<object, float?> CableLabelRadius;
            public TryGetLabelMeshFn TryGetLabelMesh;
        }

        private sealed class Adapter : ICableLabelMount
        {
            private readonly object _target;
            private readonly Binding _binding;

            public Adapter(object target, Binding binding)
            {
                _target = target;
                _binding = binding;
            }

            public float? CableLabelRadius => _binding.CableLabelRadius(_target);

            public bool TryGetLabelMesh(out Mesh mesh, out Vector3 offset, out Quaternion rotation) =>
                _binding.TryGetLabelMesh(_target, out mesh, out offset, out rotation);
        }

        private static readonly Dictionary<Type, Binding> Bindings = new Dictionary<Type, Binding>();

        // The cable's mount: the cable itself, or another component on its GameObject.
        public static bool TryGet(Cable cable, out ICableLabelMount mount)
        {
            foreach (var component in cable.GetComponents<MonoBehaviour>())
            {
                if (component != null && TryAdapt(component, out mount))
                    return true;
            }
            mount = null;
            return false;
        }

        // One object: our own interface, implemented directly (by a mod that references this
        // one), or a matching copy of it, adapted.
        internal static bool TryAdapt(object target, out ICableLabelMount mount)
        {
            if (target is ICableLabelMount own)
            {
                mount = own;
                return true;
            }
            var binding = Bind(target.GetType());
            mount = binding != null ? new Adapter(target, binding) : null;
            return mount != null;
        }

        private static Binding Bind(Type type)
        {
            if (Bindings.TryGetValue(type, out var cached))
                return cached;
            Binding binding = null;
            var contract = typeof(ICableLabelMount);
            var copy = type.GetInterfaces().FirstOrDefault(i => i.FullName == contract.FullName);
            if (copy != null)
            {
                try
                {
                    binding = new Binding
                    {
                        CableLabelRadius = BindRadius(copy),
                        TryGetLabelMesh = BindTryGetLabelMesh(copy),
                    };
                    CableLabelPrefabs.Log.LogInfo(
                        $"{type.FullName} implements {contract.FullName} ({copy.Assembly.GetName().Name})");
                }
                catch (Exception ex)
                {
                    binding = null;
                    CableLabelPrefabs.Log.LogError(
                        $"{type.FullName} implements a {contract.FullName} that doesn't match this "
                        + $"mod's, so its labels use the standard fit: {ex.Message}");
                }
            }
            Bindings[type] = binding;
            return binding;
        }

        // target => ((Copy)target).CableLabelRadius
        private static Func<object, float?> BindRadius(Type copy)
        {
            var property = copy.GetProperty(nameof(ICableLabelMount.CableLabelRadius));
            if (property == null || property.PropertyType != typeof(float?) || !property.CanRead)
                throw new MissingMemberException(copy.FullName, $"float? {nameof(ICableLabelMount.CableLabelRadius)} {{ get; }}");
            var target = Expression.Parameter(typeof(object), "target");
            var body = Expression.Property(Expression.Convert(target, copy), property);
            return Expression.Lambda<Func<object, float?>>(body, target).Compile();
        }

        // (target, out mesh, out offset, out rotation) =>
        //     ((Copy)target).TryGetLabelMesh(out mesh, out offset, out rotation)
        private static TryGetLabelMeshFn BindTryGetLabelMesh(Type copy)
        {
            var name = nameof(ICableLabelMount.TryGetLabelMesh);
            var parameterTypes = new[]
            {
                typeof(Mesh).MakeByRefType(), typeof(Vector3).MakeByRefType(),
                typeof(Quaternion).MakeByRefType(),
            };
            var method = copy.GetMethod(name, parameterTypes);
            if (method == null || method.ReturnType != typeof(bool)
                || method.GetParameters().Any(p => !p.IsOut))
                throw new MissingMemberException(copy.FullName,
                    $"bool {name}(out Mesh, out Vector3, out Quaternion)");
            var target = Expression.Parameter(typeof(object), "target");
            var mesh = Expression.Parameter(parameterTypes[0], "mesh");
            var offset = Expression.Parameter(parameterTypes[1], "offset");
            var rotation = Expression.Parameter(parameterTypes[2], "rotation");
            var body = Expression.Call(Expression.Convert(target, copy), method, mesh, offset, rotation);
            return Expression.Lambda<TryGetLabelMeshFn>(body, target, mesh, offset, rotation).Compile();
        }
    }
}
