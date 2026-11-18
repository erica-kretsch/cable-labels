using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
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
    // Hand-written for this one contract. A general TryAdapt<TContract> would generate the
    // adapter type with Reflection.Emit instead. Unlike ImpromptuInterface's ActLike, which it
    // resembles, it adapts only an object that declares a copy of the contract by name, and
    // checks every member up front rather than failing at the call.
    internal static class CableLabelMounts
    {
        private delegate bool TryCreateLabelMeshFn(object target, out Mesh mesh,
            out Vector3 offset, out Quaternion rotation, out CancellationToken changed);

        // One per implementing type: the compiled members, or null where the type has no
        // matching copy (also cached, so each type is examined once).
        private sealed class Binding
        {
            public Func<object, float?> CableLabelRadius;
            public TryCreateLabelMeshFn TryCreateLabelMesh;
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

            public bool TryCreateLabelMesh(out Mesh mesh, out Vector3 offset,
                out Quaternion rotation, out CancellationToken changed) =>
                _binding.TryCreateLabelMesh(_target, out mesh, out offset, out rotation, out changed);
        }

        private static readonly Dictionary<Type, Binding> Bindings = new Dictionary<Type, Binding>();

        // The cable's mount: the cable itself, or another component on its GameObject.
        public static bool TryGet(Cable cable, out ICableLabelMount mount)
        {
            foreach (var component in cable.GetComponents<MonoBehaviour>())
            {
                if (component != null && component.TryAdapt(out mount))
                    return true;
            }
            mount = null;
            return false;
        }

        // One object: our own interface, implemented directly (by a mod that references this
        // one), or a matching copy of it, adapted. Shaped as the general version would be
        // (target.TryAdapt(out TContract contract)), so call sites stay the same if one is
        // provided by a shared library.
        internal static bool TryAdapt(this object target, out ICableLabelMount mount)
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
                        TryCreateLabelMesh = BindTryCreateLabelMesh(copy),
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

        // (target, out mesh, out offset, out rotation, out changed) =>
        //     ((Copy)target).TryCreateLabelMesh(out mesh, out offset, out rotation, out changed)
        private static TryCreateLabelMeshFn BindTryCreateLabelMesh(Type copy)
        {
            var name = nameof(ICableLabelMount.TryCreateLabelMesh);
            var parameterTypes = new[]
            {
                typeof(Mesh).MakeByRefType(), typeof(Vector3).MakeByRefType(),
                typeof(Quaternion).MakeByRefType(), typeof(CancellationToken).MakeByRefType(),
            };
            var method = copy.GetMethod(name, parameterTypes);
            if (method == null || method.ReturnType != typeof(bool)
                || method.GetParameters().Any(p => !p.IsOut))
                throw new MissingMemberException(copy.FullName,
                    $"bool {name}(out Mesh, out Vector3, out Quaternion, out CancellationToken)");
            var target = Expression.Parameter(typeof(object), "target");
            var parameters = parameterTypes.Select(t => Expression.Parameter(t)).ToArray();
            var body = Expression.Call(Expression.Convert(target, copy), method, parameters);
            return Expression.Lambda<TryCreateLabelMeshFn>(body, new[] { target }.Concat(parameters))
                .Compile();
        }
    }
}
