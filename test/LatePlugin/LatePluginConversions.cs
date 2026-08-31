using System.Collections;
using System.Collections.Generic;
using System.Threading;
using CompasPb.Data;
using CompasPb.Test.LatePlugin;

[assembly: CompasPbRegistrations(typeof(LatePluginConversions))]

namespace CompasPb.Test.LatePlugin;

/// <summary>
/// A domain type this assembly registers on its own behalf.
/// </summary>
public class LatePluginWidget
{
    public string Label { get; set; } = string.Empty;
}

public static class LatePluginConversions
{
    public const string Dtype = "compas_pb_test/LatePluginWidget";

    private static int _invocations;

    public static int Invocations => Volatile.Read(ref _invocations);

    public static void Register()
    {
        Interlocked.Increment(ref _invocations);

        // A fallback rather than a protobuf mapping: it needs no .proto of its own, and the
        // registry path it exercises is the same one.
        Registry.RegisterFallback<LatePluginWidget>(
            Dtype,
            widget => new Dictionary<string, object?> { ["label"] = widget.Label },
            values => new LatePluginWidget { Label = (string)values["label"]! }
        );
    }
}
