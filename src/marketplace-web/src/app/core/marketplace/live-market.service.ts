import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface LiveParticipant {
  id: string;
  name: string;
  profileType: 'Buyer' | 'Supplier';
  isOnline: boolean;
  city: string;
  county: string;
  isSameCity: boolean;
}
export interface LiveMarketSnapshot {
  generatedAt: string;
  participants: LiveParticipant[];
  onlineUserCount: number;
  onlineBuyerCount: number;
  onlineSupplierCount: number;
  isLocationFiltered: boolean;
  filterCity: string | null;
  filterCounty: string | null;
  approximateRadiusKm: number | null;
}

@Injectable({ providedIn: 'root' })
export class LiveMarketService {
  private readonly http = inject(HttpClient);
  getSnapshot(profileType?: 'Buyer' | 'Supplier') {
    const params = profileType ? new HttpParams().set('profileType', profileType) : undefined;
    return this.http.get<LiveMarketSnapshot>('/api/v1/piata-live', { params });
  }
}
