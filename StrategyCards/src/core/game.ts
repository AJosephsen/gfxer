export type GameState = {
  turn: number;
};

export function createInitialGameState(): GameState {
  return { turn: 1 };
}
