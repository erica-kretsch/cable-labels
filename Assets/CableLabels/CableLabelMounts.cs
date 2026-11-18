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
    /// <summary>
    /// Finds a cable's <see cref="ICableLabelMount"/>, including another mod's copy of it, and
    /// adapts it to this assembly's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A copy compiled into another assembly is a different type, so it's matched by full name.
    /// Each member must have the same name and signature; one forwarding delegate per member is
    /// compiled once per implementing type. A copy that doesn't match is logged once and ignored.
    /// </para>
    /// <para>
    /// Written for this one contract. A general <c>TryAdapt&lt;TContract&gt;</c> would generate
    /// the adapter with Reflection.Emit. Unlike ImpromptuInterface's <c>ActLike</c>, it adapts
    /// only an object that declares the contract by name, and checks every member up front.
    /// </para>
    /// </remarks>
    internal static class CableLabelMounts
    {
        private delegate bool TryCreateLabelMeshFn(object target, out Mesh mesh,
            out Vector3 offset, out Quaternion rotation, out CancellationToken changed);

        /// <summary>
        /// The compiled members of one implementing type. Cached per type, as null where the type
        /// has no matching copy.
        /// </summary>
        private sealed class Binding
        {
            public Func<object, float?> CableLabelRadius;
            public TryCreateLabelMeshFn TryCreateLabelMesh;
        }

        /// <summary>Forwards this assembly's contract to an object implementing a copy.</summary>
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

        /// <summary>
        /// The cable's mount: the cable itself, or another component on its GameObject.
        /// </summary>
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

        /// <summary>
        /// <paramref name="target"/> as an <see cref="ICableLabelMount"/>: itself if it implements
        /// this assembly's, else an adapter if it implements a matching copy.
        /// </summary>
        /// <remarks>
        /// Shaped as a general <c>target.TryAdapt(out TContract)</c> would be, so call sites stay
        /// the same if a shared library provides one.
        /// </remarks>
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

        /// <summary>The binding for <paramref name="type"/>, made the first time.</summary>
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

        /// <summary>Compiles <c>target => ((Copy)target).CableLabelRadius</c>.</summary>
        private static Func<object, float?> BindRadius(Type copy)
        {
            var property = copy.GetProperty(nameof(ICableLabelMount.CableLabelRadius));
            if (property == null || property.PropertyType != typeof(float?) || !property.CanRead)
                throw new MissingMemberException(copy.FullName, $"float? {nameof(ICableLabelMount.CableLabelRadius)} {{ get; }}");
            var target = Expression.Parameter(typeof(object), "target");
            var body = Expression.Property(Expression.Convert(target, copy), property);
            return Expression.Lambda<Func<object, float?>>(body, target).Compile();
        }

        /// <summary>
        /// Compiles <c>(target, out ...) => ((Copy)target).TryCreateLabelMesh(out ...)</c>.
        /// </summary>
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
