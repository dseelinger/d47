# Tests

## Tests are named as behavioural sentences

Not `MethodName_Condition_Result`. From the suite: `ACancelledTurnIsNotAFailureTests`,
`AStaleBuildSaysSoTests`, `TwoUnpromptedVoicesKeepTheirDistanceTests`, `SayItAndTheShipDoesItTests`.

## Locate the repository root with `d47.slnx`

Use `d47.slnx`, a build file that must exist for the test to have compiled. Keying off documentation
makes the test fail when that file moves.
