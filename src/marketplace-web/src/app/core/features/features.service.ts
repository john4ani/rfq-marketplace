import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

export interface FeaturesFlags {
  marketplaceEnabled: boolean;
  autoBiddingEnabled: boolean;
  seapIngestionEnabled: boolean;
}

/**
 * Live feature flags exposed by the API (GET /api/v1/features). MarketPlaceEnabled=false is the MVP
 * default: it hides every marketplace surface (live market, RFQs, offers, profiles) leaving only the
 * standalone Anunțuri SEAP product and the Administrator workspace visible.
 *
 * The initial value is marketplace-enabled (fail-open): when the API is unavailable the shell keeps the
 * complete navigation instead of locking the user out, per the availability UX principle. The real flag
 * from the backend (SSM features-marketplace-enabled, false in every environment) wins on first load.
 */
@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private readonly http = inject(HttpClient);
  private readonly marketplaceEnabledState = signal(true);
  private initialized = false;

  readonly marketplaceEnabled = this.marketplaceEnabledState.asReadonly();

  async initialize(): Promise<void> {
    if (this.initialized) return;
    this.initialized = true;
    try {
      const flags = await firstValueFrom(this.http.get<FeaturesFlags>('/api/v1/features').pipe(timeout(3000)));
      this.marketplaceEnabledState.set(flags.marketplaceEnabled);
    } catch {
      // Fail open: keep the current value when the flag endpoint is unavailable.
    }
  }

  /** Test seam: overrides the flag directly without an HTTP round-trip (used by the shell spec). */
  setMarketplaceEnabledForTest(enabled: boolean): void {
    this.marketplaceEnabledState.set(enabled);
    this.initialized = true;
  }
}
