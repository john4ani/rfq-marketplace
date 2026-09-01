namespace Marketplace.Application.Companies;

/// <summary>
/// User-facing vault upload validation failure (e.g. the selected file is not actually a PDF). The
/// API controller maps it to a Validation Problem so the SPA can show the Romanian message verbatim.
/// </summary>
public sealed class VaultValidationException(string message) : Exception(message);
