namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class AuthorityStateModelScenario : IVirtualizationScenario
{
    public string Id => "authority-state-model";
    public string Description => "Reference model separating result, completion and retire authority.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        int rotation = (int)(unchecked((uint)context.Profile.Seed) & 7U);
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int grantMask = (iteration + rotation) & 7;
            var grants = new AuthorityGrants(
                CanProduceResult: (grantMask & 1) != 0,
                CanPublishCompletion: (grantMask & 2) != 0,
                CanPublishRetire: (grantMask & 4) != 0);
            var model = new AuthorityTransitionModel(grants);

            bool result = model.TryProduceResult();
            bool completion = model.TryPublishCompletion();
            bool retire = model.TryPublishRetire();
            context.Check(result == grants.CanProduceResult, "result transition must require result authority");
            context.Check(completion == (grants.CanProduceResult && grants.CanPublishCompletion),
                "completion must require a produced result and independent completion authority");
            context.Check(retire == (grants.CanProduceResult && grants.CanPublishCompletion && grants.CanPublishRetire),
                "retire must require completion and independent retire authority");
            context.Check(!model.BackendExecutionAuthorized,
                "reference authority model must never imply production backend authorization");

            context.Count(result ? "model_result_allowed" : "model_result_denied");
            context.Count(completion ? "model_completion_allowed" : "model_completion_denied");
            context.Count(retire ? "model_retire_allowed" : "model_retire_denied");
            context.Count($"grant_pattern_{grantMask}");
            context.Trace("authority-state-model",
                ("grants", grants),
                ("result", result),
                ("completion", completion),
                ("retire", retire),
                ("finalState", model.State));
            context.CompleteIteration("Reference authority transition sequence completed.");
        }

        return Task.CompletedTask;
    }

    private sealed record AuthorityGrants(bool CanProduceResult, bool CanPublishCompletion, bool CanPublishRetire);

    private enum AuthorityState
    {
        Requested,
        ResultProduced,
        CompletionPublished,
        Retired,
    }

    private sealed class AuthorityTransitionModel(AuthorityGrants grants)
    {
        public AuthorityState State { get; private set; } = AuthorityState.Requested;
        public bool BackendExecutionAuthorized => false;

        public bool TryProduceResult()
        {
            if (!grants.CanProduceResult || State != AuthorityState.Requested)
                return false;
            State = AuthorityState.ResultProduced;
            return true;
        }

        public bool TryPublishCompletion()
        {
            if (!grants.CanPublishCompletion || State != AuthorityState.ResultProduced)
                return false;
            State = AuthorityState.CompletionPublished;
            return true;
        }

        public bool TryPublishRetire()
        {
            if (!grants.CanPublishRetire || State != AuthorityState.CompletionPublished)
                return false;
            State = AuthorityState.Retired;
            return true;
        }
    }
}
