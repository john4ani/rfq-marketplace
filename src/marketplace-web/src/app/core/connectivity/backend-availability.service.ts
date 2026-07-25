import { Injectable, signal } from '@angular/core';

export type BackendStatus = 'unknown' | 'available' | 'unavailable';

@Injectable({ providedIn: 'root' })
export class BackendAvailabilityService {
  private readonly statusState = signal<BackendStatus>('unknown');
  private readonly retryingState = signal(false);

  readonly status = this.statusState.asReadonly();
  readonly retrying = this.retryingState.asReadonly();

  shouldBlockRequest(url: string): boolean {
    return url.startsWith('/api/') && this.statusState() === 'unavailable';
  }

  markAvailable(): void {
    this.statusState.set('available');
  }

  markUnavailable(): void {
    this.statusState.set('unavailable');
  }

  async retry(): Promise<void> {
    if (this.retryingState())
      return;

    this.retryingState.set(true);
    try {
      const response = await fetch('/health/ready', { cache: 'no-store' });
      if (!response.ok)
        throw new Error(`Backend readiness returned ${response.status}.`);

      this.markAvailable();
      globalThis.location.reload();
    } catch {
      this.markUnavailable();
    } finally {
      this.retryingState.set(false);
    }
  }
}
