# StrategyCards

Microscopic C# setup for fast TDD cycles.

## Structure

- `StrategyCards.Core/` class library
- `StrategyCards.Core.Tests/` xUnit test project
- `StrategyCards.slnx` solution

## Commands

- `dotnet test`
- `dotnet test --no-build`

## TDD Loop

1. Write one small failing test in `StrategyCards.Core.Tests/`.
2. Implement minimum code in `StrategyCards.Core/`.
3. Refactor while tests stay green.
