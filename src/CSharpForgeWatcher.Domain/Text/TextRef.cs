namespace CSharpForgeWatcher.Domain.Text;

/// <summary>
/// Message destiné à l'utilisateur, désigné par sa <b>clé</b> et ses arguments — jamais par
/// sa formulation (SPEC-UI-LANG-002).
/// </summary>
/// <remarks>
/// <para>
/// C'est ce qui permet au domaine et aux règles de détection de dire <i>ce qui s'est passé</i>
/// sans choisir <i>dans quelle langue</i> le dire. La formulation appartient au catalogue de
/// la couche application, et la langue effective n'est connue qu'à l'affichage : un même
/// événement mémorisé se relit dans la langue courante, même si elle a changé depuis.
/// </para>
/// <para>
/// Un argument peut être un <see cref="TextRef"/> : un fragment optionnel — le fichier
/// commenté, le nombre de messages suivants — se compose alors sans obliger chaque langue à
/// partager la même découpe de phrase.
/// </para>
/// </remarks>
public sealed record TextRef
{
    /// <summary>Fragment vide, pour une partie facultative absente.</summary>
    public static readonly TextRef Empty = new() { Key = TextKeys.Empty };

    /// <summary>Clé du message dans le catalogue.</summary>
    public required string Key { get; init; }

    /// <summary>Arguments de mise en forme, dans l'ordre des marqueurs <c>{0}</c>, <c>{1}</c>…</summary>
    public IReadOnlyList<object?> Arguments { get; init; } = Array.Empty<object?>();

    /// <summary>Message sans argument.</summary>
    public static TextRef Of(string key) => new() { Key = key };

    /// <summary>Message et ses arguments.</summary>
    public static TextRef Of(string key, params object?[] arguments)
        => new() { Key = key, Arguments = arguments };

    /// <summary>
    /// Représentation de diagnostic. <b>Jamais</b> ce qu'on affiche à l'utilisateur : seul le
    /// catalogue sait formuler.
    /// </summary>
    public override string ToString()
        => Arguments.Count == 0 ? Key : $"{Key}({string.Join(", ", Arguments)})";
}
