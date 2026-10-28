namespace FixFlow.Application.Email;

/// <summary>Where sending an email failed (<see cref="EmailDeliveryException"/>).</summary>
public enum EmailFailureKind
{
    /// <summary>No connection to the SMTP server: refused, unreachable, or dropped.</summary>
    Connect,

    /// <summary>The TLS handshake failed: the server's certificate, or no common protocol.</summary>
    Tls,

    /// <summary>The server rejected the credentials, or required some that weren't given.</summary>
    Authentication,

    /// <summary>The server answered with an error status: a rejected sender or recipient, a full mailbox.</summary>
    Command,

    /// <summary>The server's reply didn't follow SMTP.</summary>
    Protocol,

    /// <summary>The server didn't answer in time.</summary>
    Timeout,

    /// <summary>
    /// The server lacks an extension that sending needs: STARTTLS (<c>Smtp:Security</c>), authentication
    /// (<c>Smtp:UserName</c>), or internationalized addresses.
    /// </summary>
    Unsupported,
}
