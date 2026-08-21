using System;

namespace CompasPb;

/// <summary>
/// Marks an assembly as containing protobuf types for the compas_pb registry.
/// Apply this attribute to opt into future automatic discovery.
/// Today, you must still call <see cref="Data.Registry.RegisterAssembly"/> explicitly.
/// </summary>
/// <example>
/// <code>[assembly: CompasPb.CompasPbRegistration]</code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class CompasPbRegistrationAttribute : Attribute { }
