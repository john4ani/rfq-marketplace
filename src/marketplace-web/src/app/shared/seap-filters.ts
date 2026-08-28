// Deterministic SEAP filter helpers shared by the onboarding chat and the Anunțuri SEAP advanced filter
// editor. They mirror the backend Domain helpers (CpvPrefix / RomanianCountyCodes) so codes and names are
// normalized to the canonical values the database filters by.

export const ROMANIAN_COUNTY_CODES: Record<string, string> = {
  AB: 'Alba', AR: 'Arad', AG: 'Argeș', BC: 'Bacău', BH: 'Bihor', BN: 'Bistrița-Năsăud', BT: 'Botoșani',
  BV: 'Brașov', BR: 'Brăila', B: 'București', BZ: 'Buzău', CS: 'Caraș-Severin', CL: 'Călărași',
  CJ: 'Cluj', CT: 'Constanța', CV: 'Covasna', DB: 'Dâmbovița', DJ: 'Dolj', GL: 'Galați', GR: 'Giurgiu',
  GJ: 'Gorj', HR: 'Harghita', HD: 'Hunedoara', IL: 'Ialomița', IS: 'Iași', IF: 'Ilfov',
  MM: 'Maramureș', MH: 'Mehedinți', MS: 'Mureș', NT: 'Neamț', OT: 'Olt', PH: 'Prahova',
  SJ: 'Sălaj', SM: 'Satu Mare', SB: 'Sibiu', SV: 'Suceava', TR: 'Teleorman', TM: 'Timiș',
  TL: 'Tulcea', VS: 'Vaslui', VL: 'Vâlcea', VN: 'Vrancea'
};

/** Returns the canonical county name for a code ("IS") or name ("Iasi"/"Iași"), or null when invalid. */
export function normalizeCounty(value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const byCode = ROMANIAN_COUNTY_CODES[trimmed.toUpperCase()];
  if (byCode) return byCode;
  const folded = foldCounty(trimmed);
  const match = Object.values(ROMANIAN_COUNTY_CODES).find(county => foldCounty(county) === folded);
  return match ?? null;
}

/** Returns the digit CPV prefix (at most 8 digits), or null when the value carries no digits. */
export function normalizeCpvPrefix(value: string): string | null {
  const digits = (value.match(/\d/g) ?? []).slice(0, 8).join('');
  return digits.length > 0 ? digits : null;
}

function foldCounty(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLocaleLowerCase('ro-RO')
    .replace(/-/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}
