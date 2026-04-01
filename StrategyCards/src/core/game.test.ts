import { describe, expect, it } from 'vitest';
import { createInitialGameState } from './game';

describe('createInitialGameState', () => {
  it('starts at turn 1', () => {
    expect(createInitialGameState()).toEqual({ turn: 1 });
  });
});
