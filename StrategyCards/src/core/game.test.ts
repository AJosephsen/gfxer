import { describe, expect, it } from 'vitest';
import { createInitialGameState, Game } from './game';

describe('createInitialGameState', () => {
  it('starts at turn 1', () => {
    expect(createInitialGameState()).toEqual({ turn: 1 });
  });
});

describe('Game', () => {
  it('creates with name and guid', () => {
    const game = new Game('NameOfGame');

    expect(game.name).toBe('NameOfGame');
    expect(game.guid).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
    );
  });
});
