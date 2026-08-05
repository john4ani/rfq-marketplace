import { unitLabel } from './unit-label';

describe('unitLabel', () => {
  it('provides Romanian labels for every persisted unit value', () => {
    expect([
      'Piece', 'Pack', 'Bag', 'Kilogram', 'Tonne', 'Metre',
      'SquareMetre', 'CubicMetre', 'Litre', 'Pallet', 'Other'
    ].map(unit => unitLabel(unit as Parameters<typeof unitLabel>[0]))).toEqual([
      'buc.', 'pachete', 'saci', 'kg', 'tone', 'm', 'm²', 'm³', 'l', 'paleți', 'unități'
    ]);
  });
});
