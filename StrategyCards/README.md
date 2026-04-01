# StrategyCards

Microscopic TypeScript web shell for fast TDD cycles.

## Commands

- `npm install`
- `npm run test:watch` for red/green loop
- `npm run dev` for browser shell
- `npm run typecheck` for strict TS checks

## TDD Loop

1. Write one small failing test in `src/core/*.test.ts`.
2. Implement minimum code in `src/core/*.ts`.
3. Refactor while keeping tests green.
