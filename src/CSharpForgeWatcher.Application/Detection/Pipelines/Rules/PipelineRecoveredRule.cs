using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Pipelines.Rules;

/// <summary>
/// SPEC-PIPE-002 — un pipeline surveillé repasse au vert après un échec.
/// </summary>
/// <remarks>
/// Sans cet événement, l'utilisateur alerté d'un échec n'apprend jamais que le problème est
/// réglé autrement qu'en allant vérifier. C'est la contrepartie indispensable de
/// <see cref="PipelineFailedRule"/>.
/// </remarks>
public sealed class PipelineRecoveredRule : IPipelineEventRule
{
    /// <inheritdoc />
    public string Name => "Retour au vert";

    /// <inheritdoc />
    public IEnumerable<PipelineEvent> Detect(PipelineDetectionContext context)
    {
        if (!context.IsNewRun || context.Previous is not { } previous)
        {
            yield break;
        }

        // Le retour au vert n'a de sens que si l'on venait du rouge.
        if (!previous.WasFailing || !context.Run.IsSuccess)
        {
            yield break;
        }

        var branch = string.IsNullOrEmpty(context.Run.Branch)
            ? TextRef.Empty
            : TextRef.Of(TextKeys.Event.PipelineBranch, context.Run.Branch);

        yield return context.CreateEvent(
            NotificationKind.PipelineRecovered,
            TextRef.Of(TextKeys.Event.PipelineRecovered, branch, previous.LastRunName));
    }
}
