import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface BankAccount { bankName: string; iban: string; currency: string; }

export interface CompanyLegalProfile {
  legalRepresentativeName: string;
  legalRepresentativeRole: string | null;
  legalForm: string | null;
  bankAccounts: BankAccount[];
}

export interface CompanyExperience {
  id: string;
  clientName: string;
  contractTitle: string;
  contractNumberDate: string | null;
  valueAmount: number | null;
  valueCurrency: string | null;
  cpvCode: string | null;
  completionDate: string | null;
  documentRefUrl: string | null;
}

export interface CompanyCertificate {
  id: string;
  certificateType: string;
  serialNumber: string | null;
  issuer: string | null;
  validUntil: string | null;
}

export interface CompanyPersonnel {
  id: string;
  fullName: string;
  role: string;
  professionalCertificates: string[];
  cvUrl: string | null;
}

/** Read projection of the company SEAP qualification vault (legal data, portfolio, certificates, team). */
export interface CompanyQualification {
  legal: CompanyLegalProfile | null;
  experience: CompanyExperience[];
  certificates: CompanyCertificate[];
  personnel: CompanyPersonnel[];
}

export type CompanyExperienceInput = Omit<CompanyExperience, 'id'>;
export type CompanyCertificateInput = Omit<CompanyCertificate, 'id'>;
export type CompanyPersonnelInput = Omit<CompanyPersonnel, 'id'>;

/** The certificate types the backend enum accepts (names of <c>CompanyCertificateType</c>). */
export const COMPANY_CERTIFICATE_TYPES = ['Iso9001', 'Iso14001', 'Iso45001', 'Anre', 'Cnsas', 'Other'] as const;

/** Friendly Romanian labels for each certificate type, used in the UI select. */
export const COMPANY_CERTIFICATE_TYPE_LABELS: Record<string, string> = {
  Iso9001: 'ISO 9001 — Managementul calității',
  Iso14001: 'ISO 14001 — Managementul mediului',
  Iso45001: 'ISO 45001 — Sănătate și securitate ocupațională',
  Anre: 'ANRE — Autorizație furnizare',
  Cnsas: 'CNSAS',
  Other: 'Alt certificat / atestat'
};

@Injectable({ providedIn: 'root' })
export class CompanyQualificationService {
  private readonly http = inject(HttpClient);
  private readonly base = (companyId: string) => `/api/v1/companies/${companyId}/qualification`;

  get(companyId: string) { return this.http.get<CompanyQualification>(this.base(companyId)); }

  updateLegal(companyId: string, input: {
    legalRepresentativeName: string; legalRepresentativeRole: string | null; legalForm: string | null; bankAccounts: BankAccount[];
  }) { return this.http.put<CompanyLegalProfile>(`${this.base(companyId)}/legal`, input); }

  addExperience(companyId: string, input: CompanyExperienceInput) {
    return this.http.post<CompanyExperience>(`${this.base(companyId)}/experience`, input);
  }
  updateExperience(companyId: string, id: string, input: CompanyExperienceInput) {
    return this.http.put<CompanyExperience>(`${this.base(companyId)}/experience/${id}`, input);
  }
  removeExperience(companyId: string, id: string) {
    return this.http.delete<void>(`${this.base(companyId)}/experience/${id}`);
  }

  addCertificate(companyId: string, input: CompanyCertificateInput) {
    return this.http.post<CompanyCertificate>(`${this.base(companyId)}/certificates`, input);
  }
  updateCertificate(companyId: string, id: string, input: CompanyCertificateInput) {
    return this.http.put<CompanyCertificate>(`${this.base(companyId)}/certificates/${id}`, input);
  }
  removeCertificate(companyId: string, id: string) {
    return this.http.delete<void>(`${this.base(companyId)}/certificates/${id}`);
  }

  addPersonnel(companyId: string, input: CompanyPersonnelInput) {
    return this.http.post<CompanyPersonnel>(`${this.base(companyId)}/personnel`, input);
  }
  updatePersonnel(companyId: string, id: string, input: CompanyPersonnelInput) {
    return this.http.put<CompanyPersonnel>(`${this.base(companyId)}/personnel/${id}`, input);
  }
  removePersonnel(companyId: string, id: string) {
    return this.http.delete<void>(`${this.base(companyId)}/personnel/${id}`);
  }
}
