using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Tests.Doubles;

/// <summary>
/// Formule un message pour les assertions.
/// </summary>
/// <remarks>
/// Les couches basses ne produisent plus de phrases mais des clés (SPEC-UI-LANG-002).
/// Vérifier la formulation française reste la façon la plus lisible d'exprimer une attente —
/// « le message cite l'auteur » se lit mieux que « la clé est Event.Comment et l'argument 0
/// vaut Bob » —, et le test de parité du catalogue garantit que l'anglais suit.
/// </remarks>
internal static class Texte
{
    /// <summary>Formulation française d'un message.</summary>
    internal static string Fr(TextRef? text) => TextCatalogue.For(EffectiveLanguage.French).Resolve(text);

    /// <summary>Formulation anglaise d'un message.</summary>
    internal static string En(TextRef? text) => TextCatalogue.For(EffectiveLanguage.English).Resolve(text);
}
