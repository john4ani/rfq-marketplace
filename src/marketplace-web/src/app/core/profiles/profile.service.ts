import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface BuyerProfile {
  id: string; displayName: string; buyerType: 'Individual' | 'Company'; companyName: string | null;
  taxIdentifier: string | null; phoneNumber: string; defaultCity: string; defaultCounty: string; marketRadiusKm: number;
}
export interface SupplierProfile {
  id: string; tradingName: string; companyRegistrationNumber: string; taxIdentifier: string | null;
  contactPerson: string; contactEmail: string; phoneNumber: string; website: string | null;
  address: string; city: string; county: string; latitude: number | null; longitude: number | null;
  description: string | null; approvalStatus: 'Pending' | 'Approved' | 'Suspended'; isActive: boolean;
}
export interface RepresentedCompany { id: string; name: string; role: 'Proprietar' | 'Reprezentant'; }
export interface AccountTrustSummary {
  accountId: string; email: string; roles: string[]; emailVerified: boolean;
  phoneVerified: boolean; phoneVerifiedAt: string | null; companyVerified: boolean;
  companyVerifiedAt: string | null; profileCompletenessPercent: number;
  publishedRequests: number; submittedOffers: number; selectedOffers: number;
  recentActivity: { type: string; details: string | null; occurredAt: string }[];
}
export interface TrustFacts { emailVerified:boolean; phoneVerified:boolean; companyVerified:boolean; memberSince:string; publishedRequests:number; completedRequests:number; submittedOffers:number; confirmedCollaborations:number; }
export interface EmailConfirmationRequestResponse { emailSent: boolean; alreadyConfirmed: boolean; token?: string | null; }

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  getBuyer(id: string) { return this.http.get<BuyerProfile>(`/api/v1/buyers/${id}`); }
  saveBuyer(id: string, profile: Omit<BuyerProfile, 'id'>) { return this.http.put<BuyerProfile>(`/api/v1/buyers/${id}`, profile); }
  getSupplier(id: string) { return this.http.get<SupplierProfile>(`/api/v1/suppliers/${id}`); }
  getMyCompanies() { return this.http.get<RepresentedCompany[]>('/api/v1/companies/mine'); }
  saveSupplier(id: string, profile: Omit<SupplierProfile, 'id'|'approvalStatus'|'isActive'>) { return this.http.put<SupplierProfile>(`/api/v1/suppliers/${id}`, profile); }
  getTrustSummary() { return this.http.get<AccountTrustSummary>('/api/v1/trust/me'); }
  getTrustFacts() { return this.http.get<TrustFacts>('/api/v1/trust/me/facts'); }
  requestEmailConfirmation() { return this.http.post<EmailConfirmationRequestResponse>('/api/v1/auth/request-email-confirmation', {}); }
  searchTrustAccounts(search = '') { return this.http.get<AccountTrustSummary[]>(`/api/v1/admin/trust/accounts?search=${encodeURIComponent(search)}`); }
  reviewTrustAccount(id: string, review: { phoneVerified: boolean; companyVerified: boolean; note?: string | null }) { return this.http.put<AccountTrustSummary>(`/api/v1/admin/trust/accounts/${id}/verification`, review); }
}
