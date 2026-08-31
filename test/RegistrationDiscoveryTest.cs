using System;
using System.Threading;
using CompasPb;
using CompasPb.Data;
using Xunit;

// Declared on the test assembly itself, which is what exercises the discovery path: nothing
// below ever calls DiscoveredConversions.Register, so a passing test proves CompasPb invoked it.
[assembly: CompasPbRegistrations(typeof(DiscoveredConversions))]
// A second registrar that always fails, standing in for a broken plugin in the host's
// AppDomain. Discovery has to survive it: everything below depends on the registrar above
// running, and metadata order decides which of the two the sweep reaches first.
[assembly: CompasPbRegistrations(typeof(FailingConversions))]

/// <summary>
/// A domain type with no protobuf awareness, converted through registered functions.
/// </summary>
public class DiscoveredWidget
{
    public DiscoveredWidget(double x) => X = x;

    public double X { get; }
}

public static class DiscoveredConversions
{
    private static int _invocations;

    public static int Invocations => Volatile.Read(ref _invocations);

    public static void Register()
    {
        Interlocked.Increment(ref _invocations);

        // TorusData is used by no other test. Registering a domain type against a message also
        // claims that message's deserializer, so a shared registry means picking an unclaimed one.
        Registry.Register<DiscoveredWidget, TorusData>(
            widget => new TorusData { RadiusPipe = widget.X },
            message => new DiscoveredWidget(message.RadiusPipe)
        );
    }
}

public static class FailingConversions
{
    public const string FailureMessage = "This registrar always fails.";

    public static void Register() => throw new InvalidOperationException(FailureMessage);
}

public class RegistrationDiscoveryTest
{
    [Fact]
    public void DeclaredRegistrar_RunsWithoutAnExplicitStartupCall()
    {
        // Touching the serializer is the only trigger; the registrar was never called by hand.
        var serializer = new CompasPbSerializer();

        var restored = serializer.Unpack(serializer.Pack(new DiscoveredWidget(4.5)));

        var widget = Assert.IsType<DiscoveredWidget>(restored);
        Assert.Equal(4.5, widget.X);
    }

    [Fact]
    public void DeclaredRegistrar_RunsAtMostOnce()
    {
        Registry.DiscoverRegistrations();
        Registry.DiscoverRegistrations();
        Registry.DiscoverLoadedAssemblies();

        Assert.Equal(1, DiscoveredConversions.Invocations);
    }

    [Fact]
    public void FailingRegistrar_DoesNotStopTheOtherRegistrars()
    {
        Registry.DiscoverRegistrations();

        // The sweep reached DiscoveredConversions whether or not it hit the failing registrar
        // first, and nothing threw on the way there.
        Assert.Equal(1, DiscoveredConversions.Invocations);
    }

    [Fact]
    public void FailingRegistrar_IsRecordedAsADiscoveryFailure()
    {
        Registry.DiscoverRegistrations();

        var failure = Assert.Single(
            Registry.DiscoveryFailures,
            exception => exception.Message.Contains(nameof(FailingConversions))
        );
        Assert.Equal(FailingConversions.FailureMessage, failure.InnerException?.Message);
    }

    [Fact]
    public void DiscoverRegistrations_RejectsNullAssembly()
    {
        Assert.Throws<ArgumentNullException>(() => Registry.DiscoverRegistrations(null!));
    }

    [Fact]
    public void RegistrationsAttribute_RejectsNullRegistrar()
    {
        Assert.Throws<ArgumentNullException>(() => new CompasPbRegistrationsAttribute(null!));
    }

    [Fact]
    public void RegistrationsAttribute_DefaultsToRegisterMethod()
    {
        var attribute = new CompasPbRegistrationsAttribute(typeof(DiscoveredConversions));

        Assert.Equal("Register", attribute.MethodName);
        Assert.Equal(typeof(DiscoveredConversions), attribute.RegistrarType);
    }
}
