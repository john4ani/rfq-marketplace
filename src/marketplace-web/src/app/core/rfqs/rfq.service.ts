import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export type RfqStatus = 'Draft' | 'Published' | 'ReceivingOffers' | 'OfferSelected' | 'Closed' | 'Cancelled';
export type ProcurementNeedSource = 'Manual' | 'Assisted' | 'Imported' | 'Administrator';
export type ProcurementInputType = 'StructuredForm' | 'FreeText' | 'ShoppingList' | 'ProjectDescription';
export type ProcurementNeedStatus = 'Draft' | 'Published' | 'ReceivingResponses' | 'Awarded' | 'Completed' | 'Cancelled';
export type ProcurementNeedType = 'DirectPurchase' | 'ShoppingList' | 'Project';
export type RequirementKind = 'Product' | 'Service';
export type ProcurementOutcome = 'PurchasedThroughCompaSpot' | 'PurchasedFromParticipant' | 'PurchasedElsewhere' | 'Postponed' | 'Cancelled' | 'NoSuitableOffer' | 'InsufficientResponses' | 'Other';
export type ProcurementNeedAuditEventType = 'Created' | 'Updated' | 'Published' | 'OfferReceived' | 'DeadlineExtended' | 'OfferSelected' | 'Reopened' | 'DeliveryReported' | 'Completed' | 'Cancelled' | 'OutcomeRecorded' | 'Migrated';
export type FulfillmentStatus = 'NotApplicable' | 'AwaitingDelivery' | 'DeliveryReported' | 'Delivered';
export type RfqLocationContext = 'Personal' | 'Company' | 'Other';
export type UnitOfMeasure = 'Piece' | 'Pack' | 'Bag' | 'Kilogram' | 'Tonne' | 'Metre' | 'SquareMetre' | 'CubicMetre' | 'Litre' | 'Pallet' | 'Other';

export interface RfqItem {
  id?: string; itemNumber?: number; productName: string; manufacturerOrBrand: string | null;
  specification: string | null; quantity: number; unit: UnitOfMeasure;
  alternativesAccepted: boolean; buyerNotes: string | null; categoryId: string | null; kind: RequirementKind;
}
export interface RfqDraft {
  source: ProcurementNeedSource; inputType: ProcurementInputType;
  title: string; projectName: string | null; description: string | null; primaryCategoryId: string;
  categoryIds: string[]; needType: ProcurementNeedType; projectTemplateId: string | null;
  deliveryAddress: string; deliveryLocality: string; county: string; supplierSearchRadiusKm: number; latitude: number | null; longitude: number | null;
  locationContext?: RfqLocationContext; buyingCompanyId?: string | null;
  preferredDeliveryDate: string | null; quotationDeadline: string; alternativeProductsAccepted: boolean;
  estimatedBudget: number | null; buyerNotes: string | null; items: RfqItem[]; allowSplit: boolean; deliveryRequired: boolean;
}
export interface Rfq extends RfqDraft {
  id: string; buyerId: string; buyingCompanyId: string | null; locationContext: RfqLocationContext; status: RfqStatus; createdAt: string; updatedAt: string;
  needStatus: ProcurementNeedStatus;
  projectContext: { name: string | null; description: string | null } | null;
  deliveryConstraints: { address: string; locality: string; county: string; supplierSearchRadiusKm: number; latitude: number | null; longitude: number | null; preferredDeliveryDate: string | null; transportRequired: boolean };
  outcome: ProcurementOutcome | null; outcomeReason: string | null; outcomeRecordedAt: string | null;
  publishedAt: string | null; closedAt: string | null; selectedOfferId: string | null; reopenCount: number;
  fulfillmentStatus: FulfillmentStatus; deliveryReportedAt: string | null; deliveredAt: string | null;
}
export interface ProcurementNeedAuditEvent {
  id: string; type: ProcurementNeedAuditEventType; actorId: string;
  fromStatus: ProcurementNeedStatus | null; toStatus: ProcurementNeedStatus;
  details: string | null; occurredAt: string;
}

@Injectable({ providedIn: 'root' })
export class RfqService {
  private readonly http = inject(HttpClient);
  getMine() { return this.http.get<Rfq[]>('/api/v1/rfqs/mine'); }
  get(id: string) { return this.http.get<Rfq>(`/api/v1/rfqs/${id}`); }
  getHistory(id: string) { return this.http.get<ProcurementNeedAuditEvent[]>(`/api/v1/rfqs/${id}/history`); }
  create(draft: RfqDraft) { return this.http.post<Rfq>('/api/v1/rfqs', draft); }
  update(id: string, draft: RfqDraft) { return this.http.put<Rfq>(`/api/v1/rfqs/${id}`, draft); }
  publish(id: string) { return this.http.post<Rfq>(`/api/v1/rfqs/${id}/publish`, {}); }
  cancel(id: string) { return this.http.post<Rfq>(`/api/v1/rfqs/${id}/cancel`, {}); }
  recordOutcome(id: string, outcome: ProcurementOutcome, reason: string | null) {
    return this.http.post<Rfq>(`/api/v1/rfqs/${id}/outcome`, { outcome, reason });
  }
  extendDeadline(id: string, quotationDeadline: string) {
    return this.http.post<Rfq>(`/api/v1/rfqs/${id}/extend-deadline`, { quotationDeadline });
  }
}
