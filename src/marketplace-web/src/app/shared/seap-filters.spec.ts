import { describe, expect, it } from 'vitest';
import { normalizeCounty, normalizeCpvPrefix } from './seap-filters';

describe('seap-filters', () => {
  it.each([
    ['IS', 'Iași'],
    ['B', 'București'],
    ['Iasi', 'Iași'],
    ['IAȘI', 'Iași'],
    ['Cluj-Napoca', 'Cluj'],
    ['CLUJ NAPOCA', 'Cluj'],
    ['Targu Mures', 'Mureș'],
    ['Târgu-Mureș', 'Mureș'],
    ['Alba Iulia', 'Alba'],
    ['Baia Mare', 'Maramureș'],
    ['Oradea', 'Bihor']
  ])('normalizes %s to its canonical county', (value, expected) => {
    expect(normalizeCounty(value)).toBe(expected);
  });

  it.each([[''], ['   '], ['Muntenia'], ['XX']])('returns null for %s', (value) => {
    expect(normalizeCounty(value)).toBeNull();
  });

  it.each([
    ['45233000', '45233000'],
    ['45233000-1', '45233000'],
    ['45', '45']
  ])('normalizes the CPV prefix %s', (value, expected) => {
    expect(normalizeCpvPrefix(value)).toBe(expected);
  });

  it('returns null when a CPV value has no digits', () => {
    expect(normalizeCpvPrefix('Lucrări de drumuri')).toBeNull();
  });
});
