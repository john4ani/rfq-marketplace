import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export type SupplierCandidateStatus = 'Unverified' | 'EligibleForInvitation' | 'Rejected' | 'Claimed';
export type SupplierInvitationStatus = 'Active' | 'Claimed' | 'Revoked' | 'Expired';

export interface SupplierInvitationSummary {
  id:string; rfqId:string; rfqTitle:string; status:SupplierInvitationStatus;
  createdAt:string; expiresAt:string;
}
export interface SupplierCandidate {
  id:string; companyName:string; contactEmail:string|null; phoneNumber:string|null; website:string|null;
  city:string; county:string; categories:{id:string;name:string}[]; source:string; notes:string|null;
  status:SupplierCandidateStatus; createdAt:string; reviewNotes:string|null;
  claimedBySupplierId:string|null; invitations:SupplierInvitationSummary[];
}
export interface SupplierCandidateInput {
  companyName:string; contactEmail:string|null; phoneNumber:string|null; website:string|null;
  city:string; county:string; categoryIds:string[]; source:string; notes:string|null;
}
export interface DiscoveryOpportunity {
  id:string; title:string; deliveryLocality:string; county:string;
  quotationDeadline:string; primaryCategoryId:string; requirementCategoryIds:string[];
}
export interface SupplierInvitationCreated {
  invitationId:string; token:string; expiresAt:string;
}
export interface SupplierInvitation {
  invitationId:string; candidateId:string; companyName:string; candidateLabel:string;
  status:SupplierInvitationStatus; expiresAt:string; rfqId:string; rfqTitle:string;
  rfqDescription:string|null; deliveryLocality:string; county:string; quotationDeadline:string;
  requirementCount:number; claimed:boolean;
}

@Injectable({providedIn:'root'})
export class SupplierDiscoveryService {
  private readonly http=inject(HttpClient);
  candidates(){return this.http.get<SupplierCandidate[]>('/api/v1/admin/supplier-candidates');}
  opportunities(){return this.http.get<DiscoveryOpportunity[]>('/api/v1/admin/discovery-opportunities');}
  createCandidate(input:SupplierCandidateInput){return this.http.post<SupplierCandidate>('/api/v1/admin/supplier-candidates',input);}
  reviewCandidate(id:string,approved:boolean,notes:string){return this.http.put<SupplierCandidate>(`/api/v1/admin/supplier-candidates/${id}/review`,{approved,notes});}
  createInvitation(candidateId:string,rfqId:string){return this.http.post<SupplierInvitationCreated>(`/api/v1/admin/supplier-candidates/${candidateId}/invitations`,{rfqId});}
  invitation(token:string){return this.http.get<SupplierInvitation>(`/api/v1/supplier-invitations/${encodeURIComponent(token)}`);}
  claim(token:string){return this.http.post<SupplierInvitation>(`/api/v1/supplier-invitations/${encodeURIComponent(token)}/claim`,{});}
}
