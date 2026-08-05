using System.Security.Cryptography;
using System.Text;

namespace CSharpForgeWatcher.Application;

/// <summary>
/// Empreinte courte, stable et opaque d'une chaîne.
/// </summary>
/// <remarks>
/// <para>
/// Deux besoins s'en servent : réduire une clé de déduplication à la longueur qu'accepte un
/// canal de notification (<see cref="Notifications.NotificationTag"/>), et faire figurer un
/// jeton chiffré dans une empreinte de configuration sans l'y recopier
/// (<c>WatchedAccount.MonitoringSignature</c>).
/// </para>
/// <para>
/// <b>Stable</b> est le mot important : la valeur doit être la même d'une exécution à
/// l'autre, ce qu'aucune fonction de hachage intégrée au runtime ne garantit —
/// <see cref="string.GetHashCode()"/> est volontairement aléatoire par processus. SHA-256
/// n'est pas employé ici pour ses propriétés cryptographiques mais pour cette stabilité,
/// et parce qu'une collision y est hors de portée.
/// </para>
/// </remarks>
public static class StableHash
{
    /// <summary>Longueur de l'empreinte produite.</summary>
    public const int Length = 64;

    /// <summary>Empreinte hexadécimale minuscule de 64 caractères.</summary>
    public static string Of(string? value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));
}
