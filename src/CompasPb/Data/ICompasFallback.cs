using System.Collections;

namespace CompasPb.Data;

/// <summary>
/// Contract for COMPAS Data objects that serialize through the fallback envelope.
/// </summary>
public interface ICompasFallback
{
    IDictionary ToFallbackData();
}
