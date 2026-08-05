using System.Globalization;

namespace CSharpForgeWatcher.Domain.PullRequests;

/// <summary>
/// Identifie une pull request de façon stable, tous projets et dépôts confondus.
/// </summary>
/// <remarks>
/// Les numéros de PR sont uniques par organisation, mais on conserve l'identifiant du
/// dépôt dans la clé : cela permet de purger l'état d'un dépôt retiré de la configuration
/// sans avoir à relire quoi que ce soit (SPEC-CFG-002, règle 3).
/// <para>
/// La représentation texte <c>repoId:prId</c> sert de clé de dictionnaire JSON dans le
/// fichier d'état.
/// </para>
/// </remarks>
public readonly record struct PullRequestKey(string RepositoryId, int PullRequestId)
{
    private const char Separator = ':';

    /// <summary>Forme sérialisable « repoId:prId ».</summary>
    public override string ToString()
        => string.Concat(RepositoryId, Separator.ToString(), PullRequestId.ToString(CultureInfo.InvariantCulture));

    /// <summary>Relit une clé produite par <see cref="ToString"/>.</summary>
    public static bool TryParse(string? value, out PullRequestKey key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separatorIndex = value.LastIndexOf(Separator);
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(
                value.AsSpan(separatorIndex + 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var id))
        {
            return false;
        }

        key = new PullRequestKey(value[..separatorIndex], id);
        return true;
    }
}
