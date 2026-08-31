using System;
using System.IO;
using System.Reflection;
using CompasPb;
using Xunit;

/// <summary>
/// Covers the assemblies a startup sweep cannot see.
/// </summary>
/// <remarks>
/// The runtime loads a referenced assembly lazily, on first use of one of its types, so
/// <see cref="AppDomain.GetAssemblies"/> reports only what the process has touched. A domain
/// package the host has referenced but not yet used is therefore invisible to the sweep that
/// runs when the registry initializes, and its registrar has to be picked up later.
/// </remarks>
public class LateAssemblyDiscoveryTest
{
    private const string PluginAssemblyName = "CompasPb.Test.LatePlugin";

    [Fact]
    public void LateLoadedAssembly_RegistersWithoutAnExplicitDiscoveryCall()
    {
        // Packing first both proves the plugin was not loaded on our behalf and puts the registry
        // in the state that used to lose the registration: initialized, with its sweep behind it.
        Assert.Contains("intValue", Pack(1));
        Assert.DoesNotContain(
            AppDomain.CurrentDomain.GetAssemblies(),
            assembly => assembly.GetName().Name == PluginAssemblyName
        );

        var plugin = Assembly.LoadFrom(
            Path.Combine(AppContext.BaseDirectory, PluginAssemblyName + ".dll")
        );

        // Nothing has called into the registry since the load, so the registrar can only have run
        // because CompasPb noticed the assembly arriving.
        var conversions = plugin.GetType(PluginAssemblyName + ".LatePluginConversions", true)!;
        Assert.Equal(1, conversions.GetProperty("Invocations")!.GetValue(null));

        var widget = Activator.CreateInstance(
            plugin.GetType(PluginAssemblyName + ".LatePluginWidget", true)!
        )!;
        widget.GetType().GetProperty("Label")!.SetValue(widget, "late");

        string json = Pack(widget);

        Assert.Contains((string)conversions.GetField("Dtype")!.GetRawConstantValue()!, json);
        Assert.Contains("late", json);

        var restored = new CompasPbSerializer().UnpackJson(json);
        Assert.Equal(widget.GetType(), restored?.GetType());

        static string Pack(object value) => new CompasPbSerializer().PackAsJson(value);
    }
}
