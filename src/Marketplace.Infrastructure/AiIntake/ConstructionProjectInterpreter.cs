using Marketplace.Application.AiIntake;

namespace Marketplace.Infrastructure.AiIntake;

// Intentionally thin: construction owns no new state until Task 9. It shares the configured OpenAI transport
// through the general interpreter, while remaining a separately injectable/routable application boundary.
internal sealed class ConstructionProjectInterpreter(IGeneralNeedInterpreter generalNeedInterpreter) : IConstructionProjectInterpreter
{
    public bool IsAvailable => generalNeedInterpreter.IsAvailable;

    public async Task<ConstructionProjectInterpretation> InterpretAsync(
        ConstructionProjectInterpretationRequest request,
        CancellationToken cancellationToken)
    {
        GeneralNeedInterpretation result = await generalNeedInterpreter.InterpretAsync(new(
            request.UserId,
            request.Description,
            string.Empty,
            string.Empty,
            30,
            request.Categories,
            [],
            null,
            request.Now), cancellationToken);

        return new(result.Draft.Title, result.Clarification, result.Provider, result.Model,
            result.PromptVersion, result.InputTokens, result.OutputTokens);
    }
}
