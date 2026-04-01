export abstract class Card {
  public constructor(public readonly id: string) {}
}

export abstract class Terrain extends Card {
  public readonly kind = 'terrain' as const;

  public constructor(id: string, public readonly terrainType: string) {
    super(id);
  }
}

export class Plains extends Terrain {
  public constructor(id: string) {
    super(id, 'plains');
  }
}
