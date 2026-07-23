namespace SSDP.UPnP.PCL;

/// <summary>
/// The exception thrown for SSDP-specific configuration and protocol errors.
/// </summary>
public class SSDPException : Exception
{
    /// <summary>Creates the exception without a message.</summary>
    public SSDPException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public SSDPException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    public SSDPException(string message, Exception inner) : base(message, inner)
    {
    }
}
