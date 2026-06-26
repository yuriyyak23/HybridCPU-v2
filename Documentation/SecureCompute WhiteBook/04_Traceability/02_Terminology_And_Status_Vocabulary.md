# Terminology And Status Vocabulary

## Required Status Terms

`implemented`
: Production code exists for the bounded behavior named in the same sentence.

`revalidated`
: Existing behavior has current tests and source proof; no new authority is implied.

`policy admission`
: Policy prerequisites passed. Backend execution and publication remain independently closed.

`projection-only`
: A named read-only compatibility value may be returned after its dedicated gates. No mutation or backend authority follows.

`proof-only`
: Owner/proof-chain checks passed while execution remains false.

`admitted-denied`
: The operation was recognized and admitted to a denial boundary. It did not execute.

`design fence`
: Data shape and monotonicity are modeled, but execution is not implemented.

`future-gated`
: Behavior remains unavailable until the named RFC, implementation, tests and release gate pass.

`limited activation`
: A future release claim for one named owner-specific path after the
limited-release gate. It does not mean feature completeness.

## Forbidden Collapses

- descriptor materialized != backend available;
- grant accepted != instruction capability;
- evidence visible != runtime authority;
- owner accepted != projection allowed;
- projection allowed != mutation allowed;
- I/O admitted != device side effect completed;
- fence present != publication authorized;
- completion published != retire published;
- RFC accepted != runtime activated;
- tests green != product activation.

## Source Of Truth Rule

When sources disagree, resolve them in this order:

1. current production code;
2. current executable tests and source guards;
3. `SecureComputeActivationPlan`;
4. this WhiteBook;
5. historical Plan/Plan2/docs.

The ActivationPlan remains the normative description of SecureCompute development order and release gates. Code and tests remain the factual evidence of implemented behavior.
