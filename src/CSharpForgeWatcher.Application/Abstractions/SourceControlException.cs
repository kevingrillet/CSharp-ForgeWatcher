using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Abstractions;

/// <summary>
/// Échec d'un appel à la forge, exprimé sans dépendance à HTTP.
/// </summary>
/// <remarks>
/// La classification (<see cref="IsTransient"/>, <see cref="IsAuthenticationFailure"/>)
/// est portée par l'exception elle-même : c'est ce qui permet à
/// <see cref="Resilience.ResilientSourceControlGateway"/> de décider de réessayer sans rien
/// savoir du protocole (SPEC-POLL-005).
/// </remarks>
public class SourceControlException : Exception
{
    /// <summary>Crée une exception d'appel à la forge.</summary>
    /// <param name="text">
    /// Message destiné à l'utilisateur, désigné par sa clé : l'adaptateur dit ce qui a échoué,
    /// l'interface le dit dans la langue de l'utilisateur (SPEC-UI-LANG-002).
    /// </param>
    /// <param name="statusCode">Code HTTP, ou <c>null</c> pour une panne réseau / un délai dépassé.</param>
    /// <param name="innerException">Exception d'origine.</param>
    public SourceControlException(TextRef text, int? statusCode = null, Exception? innerException = null)
        : base(text?.ToString(), innerException)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        StatusCode = statusCode;
    }

    /// <summary>
    /// Message destiné à l'utilisateur.
    /// </summary>
    /// <remarks>
    /// <see cref="Exception.Message"/> en porte la forme de diagnostic — clé et arguments —,
    /// qui va au journal ; c'est cette propriété qui sert à l'affichage.
    /// </remarks>
    public TextRef Text { get; }

    /// <summary>Code HTTP renvoyé, ou <c>null</c> si la requête n'a pas abouti.</summary>
    public int? StatusCode { get; }

    /// <summary>Jeton invalide, expiré ou aux droits insuffisants (401 / 403).</summary>
    public bool IsAuthenticationFailure => StatusCode is 401 or 403;

    /// <summary>Ressource absente (404) : inutile de réessayer.</summary>
    public bool IsNotFound => StatusCode == 404;

    /// <summary>
    /// Panne probablement passagère : réseau injoignable, délai dépassé, quota
    /// (429) ou erreur serveur (5xx).
    /// </summary>
    public bool IsTransient
        => StatusCode is null || StatusCode.Value is 408 or 429 || StatusCode.Value >= 500;

    /// <summary>Message enrichi d'un conseil quand la cause est identifiable.</summary>
    public TextRef ToUserText() => this switch
    {
        { IsAuthenticationFailure: true } => TextRef.Of(TextKeys.Forge.AuthAdvice, Text),
        { IsTransient: true } => TextRef.Of(TextKeys.Forge.TransientAdvice, Text),
        _ => Text,
    };
}
