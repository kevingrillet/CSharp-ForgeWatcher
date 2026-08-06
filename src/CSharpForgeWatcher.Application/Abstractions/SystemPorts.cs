namespace CSharpForgeWatcher.Application.Abstractions;

/// <summary>
/// Source de temps. Injectée partout où une date est nécessaire, afin qu'aucun test
/// n'ait à dépendre de l'horloge réelle.
/// </summary>
public interface IClock
{
    /// <summary>Instant courant, en UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Attente asynchrone. Extraite derrière un port pour que les tests de réessai
/// s'exécutent instantanément (SPEC-POLL-005).
/// </summary>
public interface IDelayScheduler
{
    /// <summary>Attend la durée demandée.</summary>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>Chiffrement local d'un secret (patron Strategy sur le stockage du PAT, ADR-0002).</summary>
public interface ISecretProtector
{
    /// <summary>Chiffre une valeur en clair et retourne une forme stockable en texte.</summary>
    string Protect(string plainText);

    /// <summary>
    /// Déchiffre une valeur produite par <see cref="Protect"/>.
    /// Retourne <c>false</c> si la valeur est illisible (fichier venu d'une autre
    /// machine ou d'un autre compte Windows) — sans lever d'exception (SPEC-CFG-001).
    /// </summary>
    bool TryUnprotect(string protectedText, out string plainText);
}

/// <summary>Ouverture d'une URL dans le navigateur par défaut (SPEC-NOTIF-001).</summary>
public interface IBrowserLauncher
{
    /// <summary>Ouvre l'URL indiquée.</summary>
    void Open(string url);
}

/// <summary>
/// Lecture de l'apparence choisie dans Windows (SPEC-UI-THEME-002).
/// </summary>
/// <remarks>
/// Derrière un port parce que c'est une lecture de registre : la logique de résolution du
/// thème (<see cref="Theming.ThemeResolver"/>) reste ainsi testable sans machine Windows.
/// </remarks>
public interface ISystemThemeProbe
{
    /// <summary>
    /// Vrai si Windows est réglé en mode sombre pour les applications.
    /// Retourne <c>false</c> si l'information n'est pas lisible.
    /// </summary>
    bool PrefersDarkTheme();
}

/// <summary>Démarrage automatique avec la session Windows (SPEC-CFG-006).</summary>
public interface IAutoStartService
{
    /// <summary>Indique si le démarrage automatique est actuellement actif.</summary>
    bool IsEnabled();

    /// <summary>Active ou désactive le démarrage automatique.</summary>
    /// <returns><c>true</c> si l'opération a abouti.</returns>
    bool SetEnabled(bool enabled);
}
