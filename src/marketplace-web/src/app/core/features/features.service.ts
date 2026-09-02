import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

export interface FeaturesFlags {
  marketplaceEnabled: boolean;
  autoBiddingEnabled: boolean;
  seapDirectHitEnabled: boolean;
  seapIngestionWorkerEnabled: boolean;
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
  /** Direct Hit (SCN/CN) starts fail-closed: without the API flags the form/link stay hidden. */
  private readonly seapDirectHitEnabledState = signal(false);
  /** Ingestion worker flag — Direct Hit is only reachable when the worker that drains its runs is on. */
  private readonly seapIngestionWorkerEnabledState = signal(false);
  private initialized = false;

  readonly marketplaceEnabled = this.marketplaceEnabledState.asReadonly();
  readonly seapDirectHitEnabled = this.seapDirectHitEnabledState.asReadonly();
  /** Direct Hit is available only when BOTH the surface flag and the ingestion-worker flag are on — the UI
   *  never shows a form whose runs nobody would process. */
  readonly seapDirectHitAvailable = computed(
    () => this.seapDirectHitEnabledState() && this.seapIngestionWorkerEnabledState());

  async initialize(): Promise<void> {
    if (this.initialized) return;
    this.initialized = true;
    try {
      const flags = await firstValueFrom(this.http.get<FeaturesFlags>('/api/v1/features').pipe(timeout(3000)));
      this.marketplaceEnabledState.set(flags.marketplaceEnabled);
      this.seapDirectHitEnabledState.set(flags.seapDirectHitEnabled);
      this.seapIngestionWorkerEnabledState.set(flags.seapIngestionWorkerEnabled);
    } catch {
      // Fail open for the marketplace, fail closed for Direct Hit (never show an entry that 503s).
    }
  }

  /** Test seam: overrides the flag directly without an HTTP round-trip (used by the shell spec). */
  setMarketplaceEnabledForTest(enabled: boolean): void {
    this.marketplaceEnabledState.set(enabled);
    this.initialized = true;
  }

  /** Test seam: overrides the Direct Hit flag directly without an HTTP round-trip. */
  setSeapDirectHitEnabledForTest(enabled: boolean): void {
    this.seapDirectHitEnabledState.set(enabled);
  }

  /** Test seam: overrides the ingestion-worker flag directly without an HTTP round-trip. */
  setSeapIngestionWorkerEnabledForTest(enabled: boolean): void {
    this.seapIngestionWorkerEnabledState.set(enabled);
  }
}
