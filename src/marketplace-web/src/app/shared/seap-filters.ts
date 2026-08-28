// Deterministic SEAP filter helpers shared by the onboarding chat and the Anunțuri SEAP advanced filter
// editor. They mirror the backend Domain helpers (CpvPrefix / RomanianCountyCodes) so codes, names and
// residence-city aliases are normalized to the canonical values the database filters by.

export const ROMANIAN_COUNTY_CODES: Record<string, string> = {
  AB: 'Alba', AR: 'Arad', AG: 'Argeș', BC: 'Bacău', BH: 'Bihor', BN: 'Bistrița-Năsăud', BT: 'Botoșani',
  BV: 'Brașov', BR: 'Brăila', B: 'București', BZ: 'Buzău', CS: 'Caraș-Severin', CL: 'Călărași',
  CJ: 'Cluj', CT: 'Constanța', CV: 'Covasna', DB: 'Dâmbovița', DJ: 'Dolj', GL: 'Galați', GR: 'Giurgiu',
  GJ: 'Gorj', HR: 'Harghita', HD: 'Hunedoara', IL: 'Ialomița', IS: 'Iași', IF: 'Ilfov',
  MM: 'Maramureș', MH: 'Mehedinți', MS: 'Mureș', NT: 'Neamț', OT: 'Olt', PH: 'Prahova',
  SJ: 'Sălaj', SM: 'Satu Mare', SB: 'Sibiu', SV: 'Suceava', TR: 'Teleorman', TM: 'Timiș',
  TL: 'Tulcea', VS: 'Vaslui', VL: 'Vâlcea', VN: 'Vrancea'
};

/** Residence-city (and spelling) aliases per county, uppercase and diacritics-free — mirrors the backend
 * `RomanianCountyCodes.CountyAliases` (e.g. "Cluj-Napoca" → Cluj, "Târgu-Mureș" → Mureș). */
export const ROMANIAN_COUNTY_ALIASES: Record<string, string[]> = {
  Alba: ['ALBA IULIA', 'ALBA'], Arad: ['ARAD'], 'Argeș': ['PITESTI', 'ARGES'], Bacău: ['BACAU'],
  Bihor: ['ORADEA'], 'Bistrița-Năsăud': ['BISTRITA NASAUD', 'BISTRITA'], Botoșani: ['BOTOSANI'],
  'Brașov': ['BRASOV'], 'Brăila': ['BRAILA'], București: ['BUCURESTI', 'BUCHAREST'], Buzău: ['BUZAU'],
  'Caraș-Severin': ['RESITA', 'CARAS SEVERIN'], 'Călărași': ['CALARASI'], Cluj: ['CLUJ NAPOCA', 'CLUJ'],
  'Constanța': ['CONSTANTA'], Covasna: ['SFANTU GHEORGHE', 'SFANTUL GHEORGHE'], 'Dâmbovița': ['TARGOVISTE'],
  Dolj: ['CRAIOVA'], Galați: ['GALATI'], Giurgiu: ['GIURGIU'], Gorj: ['TARGU JIU'],
  Harghita: ['MIERCUREA CIUC'], Hunedoara: ['DEVA'], Ialomița: ['SLOBOZIA'], 'Iași': ['IASI'],
  Ilfov: ['ILFOV', 'BUFTEA'], 'Maramureș': ['BAIA MARE'], Mehedinți: ['DROBETA TURNU SEVERIN'],
  'Mureș': ['TARGU MURES'], Neamț: ['PIATRA NEAMT'], Olt: ['SLATINA'], Prahova: ['PLOIESTI'],
  'Satu Mare': ['SATU MARE'], 'Sălaj': ['ZALAU'], Sibiu: ['SIBIU'], Suceava: ['SUCEAVA'],
  Teleorman: ['ALEXANDRIA'], Timiș: ['TIMISOARA', 'TIMIS'], Tulcea: ['TULCEA'], Vaslui: ['VASLUI'],
  'Vâlcea': ['RAMNICU VALCEA'], Vrancea: ['FOCSANI']
};

/** Returns the canonical county name for a code ("IS"), a name ("Iasi"/"Iași") or a residence-city alias
 * ("Cluj-Napoca" → Cluj, "Târgu Mureș" → Mureș), or null when invalid. */
export function normalizeCounty(value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const byCode = ROMANIAN_COUNTY_CODES[trimmed.toUpperCase()];
  if (byCode) return byCode;
  const folded = foldCounty(trimmed);
  const byName = Object.values(ROMANIAN_COUNTY_CODES).find(county => foldCounty(county) === folded);
  if (byName) return byName;
  for (const [county, aliases] of Object.entries(ROMANIAN_COUNTY_ALIASES)) {
    if (aliases.some(alias => foldCounty(alias) === folded)) return county;
  }
  return null;
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
