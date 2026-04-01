import { describe, expect, it } from 'vitest';
import { Plains } from './cards';

describe('Plains', () => {
  it('can be created as a terrain card', () => {
    const plains = new Plains('plains-1');

    expect(plains.id).toBe('plains-1');
    expect(plains.kind).toBe('terrain');
    expect(plains.terrainType).toBe('plains');
  });
});
