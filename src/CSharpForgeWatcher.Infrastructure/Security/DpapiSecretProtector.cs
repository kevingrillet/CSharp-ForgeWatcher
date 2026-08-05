using System.Security.Cryptography;
using System.Text;
using CSharpForgeWatcher.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.Security;

/// <summary>
/// Chiffre le PAT avec DPAPI, portée « utilisateur courant » (ADR-0002, SPEC-CFG-001).
/// </summary>
/// <remarks>
/// La clé de chiffrement est dérivée du compte Windows : un <c>config.json</c> copié sur
/// une autre machine, ou lu par un autre utilisateur, est indéchiffrable — et
/// <see cref="TryUnprotect"/> retourne alors <c>false</c> au lieu de lever, ce qui permet
/// à l'application de simplement redemander le jeton.
/// <para>
/// L'entropie additionnelle n'est pas un secret : elle cloisonne simplement les données
/// de cette application par rapport à une autre application utilisant DPAPI.
/// </para>
/// </remarks>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ForgeWatcher/pat/v1");

    private readonly ILogger<DpapiSecretProtector>? _logger;

    /// <summary>Construit le protecteur.</summary>
    public DpapiSecretProtector(ILogger<DpapiSecretProtector>? logger = null) => _logger = logger;

    /// <inheritdoc />
    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText),
            Entropy,
            DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(encrypted);
    }

    /// <inheritdoc />
    public bool TryUnprotect(string protectedText, out string plainText)
    {
        plainText = string.Empty;

        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return false;
        }

        try
        {
            var decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedText),
                Entropy,
                DataProtectionScope.CurrentUser);

            plainText = Encoding.UTF8.GetString(decrypted);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            // Cas normal et prévu : fichier venu d'une autre machine ou d'un autre compte.
            _logger?.LogWarning(
                "Le jeton enregistré n'a pas pu être déchiffré sur ce compte Windows ; il doit être ressaisi.");
            return false;
        }
    }
}
