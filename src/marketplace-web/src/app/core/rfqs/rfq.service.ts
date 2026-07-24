import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export type RfqStatus = 'Draft' | 'Published' | 'ReceivingOffers' | 'OfferSelected' | 'Closed' | 'Cancelled';
export type UnitOfMeasure = 'Piece' | 'Pack' | 'Bag' | 'Kilogram' | 'Tonne' | 'Metre' | 'SquareMetre' | 'CubicMetre' | 'Litre' | 'Pallet' | 'Other';

export interface RfqItem {
  id?: string; itemNumber?: number; productName: string; manufacturerOrBrand: string | null;
  specification: string | null; quantity: number; unit: UnitOfMeasure;
  alternativesAccepted: boolean; buyerNotes: string | null; categoryId: string | null;
}
export interface RfqDraft {
  title: string; projectName: string | null; description: string | null; primaryCategoryId: string;
  deliveryAddress: string; deliveryLocality: string; county: string; supplierSearchRadiusKm: number; latitude: number | null; longitude: number | null;
  preferredDeliveryDate: string | null; quotationDeadline: string; alternativeProductsAccepted: boolean;
  estimatedBudget: number | null; buyerNotes: string | null; items: RfqItem[]; allowSplit: boolean;
}
export interface Rfq extends RfqDraft {
  id: string; buyerId: string; status: RfqStatus; createdAt: string; updatedAt: string;
  publishedAt: string | null; closedAt: string | null; selectedOfferId: string | null; reopenCount: number;
}

@Injectable({ providedIn: 'root' })
export class RfqService {
  private readonly http = inject(HttpClient);
  getMine() { return this.http.get<Rfq[]>('/api/v1/rfqs/mine'); }
  get(id: string) { return this.http.get<Rfq>(`/api/v1/rfqs/${id}`); }
  create(draft: RfqDraft) { return this.http.post<Rfq>('/api/v1/rfqs', draft); }
  update(id: string, draft: RfqDraft) { return this.http.put<Rfq>(`/api/v1/rfqs/${id}`, draft); }
  publish(id: string) { return this.http.post<Rfq>(`/api/v1/rfqs/${id}/publish`, {}); }
  cancel(id: string) { return this.http.post<Rfq>(`/api/v1/rfqs/${id}/cancel`, {}); }
  extendDeadline(id: string, quotationDeadline: string) {
    return this.http.post<Rfq>(`/api/v1/rfqs/${id}/extend-deadline`, { quotationDeadline });
  }
}
