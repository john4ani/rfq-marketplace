import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface LiveParticipant {
  id: string;
  name: string;
  profileType: 'Buyer' | 'Supplier';
  isOnline: boolean;
}
export interface LiveMarketSnapshot {
  generatedAt: string;
  participants: LiveParticipant[];
}

@Injectable({ providedIn: 'root' })
export class LiveMarketService {
  private readonly http = inject(HttpClient);
  getSnapshot() { return this.http.get<LiveMarketSnapshot>('/api/v1/piata-live'); }
}
