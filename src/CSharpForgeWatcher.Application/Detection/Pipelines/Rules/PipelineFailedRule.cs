using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Pipelines.Rules;

/// <summary>
/// SPEC-PIPE-001 — une exécution d'un pipeline surveillé se termine en échec.
/// </summary>
/// <remarks>
/// Le succès partiel est traité comme un échec : une étape rouge reste une étape rouge.
/// Une exécution annulée, en revanche, n'est pas un échec (règle 2).
/// </remarks>
public sealed class PipelineFailedRule : IPipelineEventRule
{
    /// <inheritdoc />
    public string Name => "Pipeline en échec";

    /// <inheritdoc />
    public IEnumerable<PipelineEvent> Detect(PipelineDetectionContext context)
    {
        // Pipeline découvert à ce cycle : on mémorise sans alerter, comme pour les PR.
        // L'utilisateur qui vient d'ajouter un pipeline déjà rouge le voit dans le menu,
        // il n'a pas besoin d'une notification sur un fait antérieur à sa demande.
        if (!context.IsNewRun)
        {
            yield break;
        }

        if (!context.Run.IsFailure)
        {
            yield break;
        }

        var branch = string.IsNullOrEmpty(context.Run.Branch)
            ? TextRef.Empty
            : TextRef.Of(TextKeys.Event.PipelineBranch, context.Run.Branch);

        var actor = context.Run.RequestedFor.Id.Length > 0
            ? TextRef.Of(TextKeys.Event.PipelineActor, context.Run.RequestedFor.SafeDisplayName)
            : TextRef.Empty;

        yield return context.CreateEvent(
            NotificationKind.PipelineFailed,
            TextRef.Of(
                TextKeys.Event.PipelineFailed,
                TextRef.Of(context.Run.Result.ToLabelKey()),
                branch,
                actor));
    }
}
