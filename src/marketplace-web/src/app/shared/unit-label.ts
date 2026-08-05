import { UnitOfMeasure } from '../core/rfqs/rfq.service';

const labels: Record<UnitOfMeasure, string> = {
  Piece: 'buc.',
  Pack: 'pachete',
  Bag: 'saci',
  Kilogram: 'kg',
  Tonne: 'tone',
  Metre: 'm',
  SquareMetre: 'm²',
  CubicMetre: 'm³',
  Litre: 'l',
  Pallet: 'paleți',
  Other: 'unități'
};

export function unitLabel(unit: UnitOfMeasure): string {
  return labels[unit];
}
