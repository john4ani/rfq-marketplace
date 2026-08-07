import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ProcurementOutcome } from '../rfqs/rfq.service';

export interface ConciergeMetric { name:string; value:number; }
export interface ConciergeRequest { id:string; title:string; buyerName:string; county:string; status:string; quotationDeadline:string; offerCount:number; hasManualMatch:boolean; }
export interface ConciergeOffer { id:string; rfqId:string; rfqTitle:string; supplierName:string; comparable:boolean; coverage:string; updatedAt:string; }
export interface AiFailureReview { eventId:string; sessionId:string; provider:string|null; model:string|null; occurredAt:string; }
export interface ConciergeOverview { metrics:ConciergeMetric[]; openRequests:ConciergeRequest[]; offersNeedingReview:ConciergeOffer[]; aiFailures:AiFailureReview[]; }
export interface ConciergeSupplier { id:string; tradingName:string; county:string; categories:string[]; }

@Injectable({providedIn:'root'})
export class ConciergeService {
  private readonly http=inject(HttpClient); private readonly base='/api/v1/admin/concierge';
  overview(){return this.http.get<ConciergeOverview>(this.base);}
  suppliers(search=''){return this.http.get<ConciergeSupplier[]>(`${this.base}/suppliers`,{params:{search}});}
  manualMatch(rfqId:string,supplierId:string,reason:string){return this.http.post<void>(`${this.base}/requests/${rfqId}/manual-match`,{supplierId,reason});}
  correction(rfqId:string,note:string){return this.http.post<void>(`${this.base}/requests/${rfqId}/request-correction`,{note});}
  normalize(offerId:string,note:string){return this.http.post<void>(`${this.base}/offers/${offerId}/request-normalization`,{note});}
  outcome(rfqId:string,outcome:ProcurementOutcome,note:string){return this.http.post<void>(`${this.base}/requests/${rfqId}/outcome`,{outcome,note:note||null});}
}
