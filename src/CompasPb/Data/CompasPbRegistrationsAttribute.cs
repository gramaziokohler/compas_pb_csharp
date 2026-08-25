using System;

namespace CompasPb.Data;

/// <summary>
/// Points CompasPb at a type that registers an assembly's conversions.
/// </summary>
/// <remarks>
/// <para>
/// Apply this at assembly level so a package's domain types work by being referenced, without the
/// host application calling into the package at startup:
/// </para>
/// <code>
/// [assembly: CompasPbRegistrations(typeof(MyPackageConversions))]
/// </code>
/// <para>
/// The named type must expose a public static method with no parameters, called
/// <c>Register</c> unless <see cref="MethodName"/> says otherwise. CompasPb invokes it at most
/// once per type. The method body should be ordinary <see cref="Registry.Register{TObject,
/// TMessage}"/> and <see cref="Registry.RegisterFallback{TObject}"/> calls, which keeps the
/// generic instantiations statically visible for ahead-of-time compilation; only the call into
/// the method is discovered.
/// </para>
/// <para>
/// Under IL2CPP or any trimming linker, preserve the registrar so the method survives stripping.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class CompasPbRegistrationsAttribute : Attribute
{
    /// <summary>
    /// The default method name invoked on the registrar type.
    /// </summary>
    public const string DefaultMethodName = "Register";

    /// <summary>
    /// Declares a type whose static registration method CompasPb should invoke.
    /// </summary>
    /// <param name="registrarType">
    /// A type exposing a public static parameterless registration method.
    /// </param>
    public CompasPbRegistrationsAttribute(Type registrarType)
    {
        RegistrarType = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
    }

    /// <summary>
    /// The type whose registration method is invoked.
    /// </summary>
    public Type RegistrarType { get; }

    /// <summary>
    /// The public static parameterless method to invoke. Defaults to <c>Register</c>.
    /// </summary>
    public string MethodName { get; set; } = DefaultMethodName;
}
