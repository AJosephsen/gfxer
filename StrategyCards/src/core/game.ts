export type GameState = {
  turn: number;
};

export function createInitialGameState(): GameState {
  return { turn: 1 };
}

export class Game {
  public readonly guid: string;
  public readonly name: string;

  public constructor(name: string) {
    this.name = name;
    this.guid = crypto.randomUUID();
  }
}
