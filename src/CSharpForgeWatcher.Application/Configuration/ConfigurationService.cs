using CSharpForgeWatcher.Application.Abstractions;

namespace CSharpForgeWatcher.Application.Configuration;

/// <summary>
/// Détient la configuration courante, la persiste et signale ses changements.
/// </summary>
/// <remarks>
/// Point unique de vérité pour la configuration (patron Observer via
/// <see cref="Changed"/>) : la fenêtre de configuration écrit ici, le minuteur et le
/// monitor lisent ici. C'est aussi le seul endroit qui manipule les jetons en clair, ce qui
/// concentre la surface de traitement des secrets — un jeton par compte depuis que plusieurs
/// forges peuvent être surveillées ensemble (SPEC-CFG-008).
/// </remarks>
public sealed class ConfigurationService
{
    private readonly IConfigurationStore _store;
    private readonly ISecretProtector _protector;
    private readonly object _gate = new();

    /// <summary>Jetons en clair, par identifiant de compte.</summary>
    private readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);

    private WatcherConfiguration _current;

    /// <summary>Charge la configuration persistée.</summary>
    public ConfigurationService(IConfigurationStore store, ISecretProtector protector)
    {
        _store = store;
        _protector = protector;
        _current = store.Load();

        // Une configuration au format 1 est convertie en un compte unique dès la lecture, puis
        // réenregistrée pour que le fichier reflète le format courant (SPEC-CFG-008).
        if (_current.Migrate())
        {
            _store.Save(_current);
        }

        LoadTokens(_current);
    }

    /// <summary>Déclenché après chaque enregistrement réussi (SPEC-CFG-004).</summary>
    public event EventHandler? Changed;

    /// <summary>Configuration active. Ne jamais muter l'instance retournée : utiliser <see cref="Edit"/>.</summary>
    public WatcherConfiguration Current
    {
        get { lock (_gate) { return _current; } }
    }

    /// <summary>Chemin du fichier de configuration (affiché dans l'onglet « Avancé »).</summary>
    public string Location => _store.Location;

    /// <summary>Jeton en clair d'un compte, ou chaîne vide s'il est absent ou illisible.</summary>
    public string TokenOf(WatchedAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        lock (_gate)
        {
            return _tokens.TryGetValue(account.Id, out var token) ? token : string.Empty;
        }
    }

    /// <summary>Validation de la configuration active.</summary>
    public ConfigurationValidationResult Validate() => Current.Validate(TokenOf);

    /// <summary>Vrai si l'application peut travailler (SPEC-CFG-003).</summary>
    public bool IsUsable => Validate().IsValid;

    /// <summary>
    /// Copie de travail à passer à la fenêtre de configuration : la modifier n'a aucun
    /// effet tant que <see cref="Apply"/> n'a pas été appelé.
    /// </summary>
    public WatcherConfiguration Edit() => Current.Clone();

    /// <summary>
    /// Enregistre une configuration éditée et notifie les abonnés.
    /// </summary>
    /// <param name="edited">Configuration issue de <see cref="Edit"/>.</param>
    /// <param name="newTokens">
    /// Jetons en clair saisis par l'utilisateur, par identifiant de compte. Un compte absent
    /// de ce dictionnaire — ou dont la valeur est vide — conserve le jeton déjà enregistré, ce
    /// qui permet de rouvrir la fenêtre de configuration sans avoir à ressaisir ses jetons
    /// (SPEC-CFG-004).
    /// </param>
    public void Apply(WatcherConfiguration edited, IReadOnlyDictionary<string, string?>? newTokens = null)
    {
        ArgumentNullException.ThrowIfNull(edited);

        lock (_gate)
        {
            foreach (var account in edited.Accounts)
            {
                var typed = newTokens is not null && newTokens.TryGetValue(account.Id, out var value)
                    ? value
                    : null;

                var known = CurrentTokenOf(account);
                var token = string.IsNullOrEmpty(typed) ? known : typed;

                if (string.IsNullOrEmpty(token))
                {
                    account.ProtectedPersonalAccessToken = string.Empty;
                }
                else if (!string.Equals(token, known, StringComparison.Ordinal)
                         || string.IsNullOrEmpty(account.ProtectedPersonalAccessToken))
                {
                    account.ProtectedPersonalAccessToken = _protector.Protect(token);
                }

                // Jeton inchangé : le brouillon porte déjà sa forme chiffrée, on la garde.
                // DPAPI produit un chiffré différent à chaque appel ; le réécrire ferait
                // passer un compte intact pour modifié aux yeux de
                // WatchedAccount.MonitoringSignature, et déclencherait un cycle inutile.
            }

            _store.Save(edited);
            _current = edited;
            LoadTokens(edited);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Paramètres de connexion d'un compte, prêts pour la fabrique de passerelle.</summary>
    public SourceControlConnection ToConnection(WatchedAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new SourceControlConnection(account.Url.Trim(), TokenOf(account), account.Provider);
    }

    /// <summary>
    /// Jeton en clair déjà connu pour ce compte : celui de la mémoire, ou celui que porte le
    /// compte édité (cas d'un compte inchangé, dont le brouillon a conservé la forme chiffrée).
    /// </summary>
    private string CurrentTokenOf(WatchedAccount account)
    {
        if (_tokens.TryGetValue(account.Id, out var known) && !string.IsNullOrEmpty(known))
        {
            return known;
        }

        return Decrypt(account.ProtectedPersonalAccessToken);
    }

    /// <summary>Déchiffre les jetons de tous les comptes.</summary>
    private void LoadTokens(WatcherConfiguration configuration)
    {
        _tokens.Clear();

        foreach (var account in configuration.Accounts)
        {
            if (string.IsNullOrEmpty(account.Id))
            {
                continue;
            }

            _tokens[account.Id] = Decrypt(account.ProtectedPersonalAccessToken);
        }
    }

    private string Decrypt(string protectedToken)
        => !string.IsNullOrEmpty(protectedToken) && _protector.TryUnprotect(protectedToken, out var plain)
            ? plain
            : string.Empty;
}
