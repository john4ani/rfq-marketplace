import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
export type OfferStatus = 'Draft'|'Submitted'|'Revised'|'Withdrawn'|'Selected'|'NotSelected';
export type OfferPricingMode = 'Itemized'|'Total';
export interface OfferItem { id?:string; rfqItemId:string; quantity:number; unitPrice:number; productDescription:string|null; available:boolean; lineTotal?:number; }
export interface OfferDraft { pricingMode:OfferPricingMode; totalPrice:number|null; deliveryCost:number; deliveryDays:number; validUntil:string; paymentTerms:string|null; notes:string|null; items:OfferItem[]; }
export interface Offer extends OfferDraft { id:string;rfqId:string;supplierId:string;status:OfferStatus;currency:string;itemsTotal:number;total:number;createdAt:string;updatedAt:string;submittedAt:string|null; }
export interface OfferSummary { id:string;rfqId:string;rfqTitle:string;quotationDeadline:string;status:OfferStatus;total:number;updatedAt:string; }
export interface BuyerOffer { supplierName:string;offer:Offer; }
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
}
