import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { TrustFacts } from '../profiles/profile.service';
export type OfferStatus = 'Draft'|'Submitted'|'Revised'|'Withdrawn'|'Selected'|'NotSelected';
export type OfferPricingMode = 'Itemized'|'Total';
export type OfferCoverage = 'Full'|'Partial';
export type OfferTaxTreatment = 'NotSpecified'|'VatIncluded'|'VatExcluded'|'NotApplicable';
export type OfferDeliveryTerms = 'IncludedInPrice'|'AddedToTotal'|'BuyerPickup'|'ArrangedSeparately';
export interface OfferItem { id?:string; rfqItemId:string; quantity:number; unitPrice:number; productDescription:string|null; available:boolean; isAlternative:boolean; lineTotal?:number; }
export interface OfferDraft { pricingMode:OfferPricingMode; totalPrice:number|null; deliveryIncluded:boolean; deliveryCost:number; deliveryDays:number; validUntil:string; paymentTerms:string|null; notes:string|null; items:OfferItem[]; coverage:OfferCoverage; taxTreatment:OfferTaxTreatment; vatRate:number|null; deliveryTerms:OfferDeliveryTerms; }
export interface Offer extends OfferDraft { id:string;rfqId:string;supplierId:string;status:OfferStatus;currency:string;itemsTotal:number;netTotal:number;vatAmount:number;total:number;hasComparableTotal:boolean;createdAt:string;updatedAt:string;submittedAt:string|null; }
export type FulfillmentStatus = 'NotApplicable'|'AwaitingDelivery'|'DeliveryReported'|'Delivered';
export interface OfferSummary { id:string;rfqId:string;rfqTitle:string;quotationDeadline:string;status:OfferStatus;coverage:OfferCoverage;taxTreatment:OfferTaxTreatment;fulfillmentStatus:FulfillmentStatus;deliveryIncluded:boolean;total:number;hasComparableTotal:boolean;updatedAt:string; }
export interface OfferCoverageDetails { requestedPositionCount:number;coveredPositionCount:number;missingRfqItemIds:string[]; }
export interface BuyerOffer { supplierName:string;offer:Offer;coverageDetails:OfferCoverageDetails;trustFacts:TrustFacts|null; }
@Injectable({providedIn:'root'})
export class OfferService {
 private readonly http=inject(HttpClient);
 getForRequest(id:string){return this.http.get<Offer|null>(`/api/v1/open-rfqs/${id}/offer`);}
 getMine(){return this.http.get<OfferSummary[]>('/api/v1/offers/mine');}
 save(id:string,draft:OfferDraft){return this.http.put<Offer>(`/api/v1/open-rfqs/${id}/offer`,draft);}
 submit(id:string){return this.http.post<Offer>(`/api/v1/open-rfqs/${id}/offer/submit`,{});}
 withdraw(id:string){return this.http.post<Offer>(`/api/v1/offers/${id}/withdraw`,{});}
 forBuyer(id:string){return this.http.get<BuyerOffer[]>(`/api/v1/rfqs/${id}/offers`);}
 select(rfqId:string,offerId:string){return this.http.post<Offer>(`/api/v1/rfqs/${rfqId}/offers/${offerId}/select`,{});}
 reopen(rfqId:string,reason:string,newQuotationDeadline:string|null){return this.http.post(`/api/v1/rfqs/${rfqId}/reopen`,{reason,newQuotationDeadline});}
 reportDelivered(rfqId:string){return this.http.post(`/api/v1/open-rfqs/${rfqId}/delivery/report`,{});}
 confirmDelivery(rfqId:string){return this.http.post(`/api/v1/rfqs/${rfqId}/delivery/confirm`,{});}
}
