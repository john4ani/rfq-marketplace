import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface BuyerProfile {
  id: string; displayName: string; buyerType: 'Individual' | 'Company'; companyName: string | null;
  taxIdentifier: string | null; phoneNumber: string; defaultCity: string; defaultCounty: string;
}
export interface SupplierProfile {
  id: string; tradingName: string; companyRegistrationNumber: string; taxIdentifier: string | null;
  contactPerson: string; contactEmail: string; phoneNumber: string; website: string | null;
  address: string; city: string; county: string; latitude: number | null; longitude: number | null;
  description: string | null; approvalStatus: 'Pending' | 'Approved' | 'Suspended'; isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  getBuyer(id: string) { return this.http.get<BuyerProfile>(`/api/v1/buyers/${id}`); }
  saveBuyer(id: string, profile: Omit<BuyerProfile, 'id'>) { return this.http.put<BuyerProfile>(`/api/v1/buyers/${id}`, profile); }
  getSupplier(id: string) { return this.http.get<SupplierProfile>(`/api/v1/suppliers/${id}`); }
  saveSupplier(id: string, profile: Omit<SupplierProfile, 'id'|'approvalStatus'|'isActive'>) { return this.http.put<SupplierProfile>(`/api/v1/suppliers/${id}`, profile); }
}
