import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { PipelineProgress } from '../companies/company-vault.service';

export interface SeapTenderAttachment {
  id: string;
  fileName: string;
  /** Authenticated API route that streams the stored file (the list endpoint may instead send the raw
   *  storage reference as s3Url — browsers cannot navigate it, so downloads always go through the API). */
  downloadUrl?: string;
}

export interface SeapTender {
  id: string;
  seapExternalId: string;
  title: string;
  description: string | null;
  cpvCodes: string[];
  cpvNames: string[];
  county: string | null;
  categoryId: string | null;
  category: { id: string; name: string } | null;
  estimatedValue: number | null;
  currency: string | null;
  buyerName: string;
  publicationDate: string;
  deadline: string;
  status: 'Imported' | 'ReadyForBidding' | 'BidGenerated' | 'Failed';
  proposalTitle: string | null;
  technicalProposalUrl: string | null;
  billOfQuantitiesUrl: string | null;
  attachments: SeapTenderAttachment[];
  /** Derived by the detail endpoint: „Procedură simplificată" for SCN notices, „Licitare deschisă" otherwise. */
  procedureType?: string;
  /** Derived by the detail endpoint from the deadline: „În desfășurare" / „Încheiată". */
  procedureState?: string;
  /** Detail fields exposed by the detail endpoint; null until the notice-detail parser extracts them. */
  awardCriterion?: string | null;
  fundingSource?: string | null;
  contractDuration?: string | null;
  participationGuarantee?: string | null;
  qualificationRequirements?: string[] | null;
  keyPersonnel?: string[] | null;
  certificates?: string[] | null;
  summaryPoints?: string[] | null;
}

export interface SeapTenderFilter {
  categoryIds?: string[];
  radiusKm?: number | null;
  county?: string | null;
  cpvPrefixes?: string[];
  counties?: string[];
  /** On-demand „Caută în SEAP": fetch matching notices from the live platform first. */
  search?: boolean;
  /** Publication-date window (yyyy-MM-dd) for the on-demand search — defaults to the current year. */
  from?: string;
  to?: string;
}

export interface BidStatus {
  status: SeapTender['status'];
  proposalTitle: string | null;
  technicalProposalUrl: string | null;
  billOfQuantitiesUrl: string | null;
}

export interface AutoBidResponse {
  ready: boolean;
  shadowMode: boolean;
  freeGeneration?: boolean;
  checkoutUrl?: string;
  sessionId?: string;
  priceEur?: number;
}

export interface TenderPricing {
  estimatedValueEur: number;
  priceEur: number;
  freeGenerationsRemaining: number;
  shadowMode: boolean;
}

export interface SeapClarificationTurn { question: string; answer: string; }

export interface SeapTenderMatchResult {
  clarificationNeeded: boolean;
  clarificationQuestion: string | null;
  extractedCpvPrefixes: string[];
  extractedCountyCodes: string[];
  extractedCounties: string[];
  summaryText: string | null;
  provider: string;
  model: string;
  promptVersion: string;
  inputTokens: number;
  outputTokens: number;
}

export interface SeapTenderMatchProfile {
  companyId: string;
  cpvPrefixes: string[];
  counties: string[];
}

export type SeapTenderMatchAvailability = { enabled: boolean; modelAvailable: boolean };

@Injectable({ providedIn: 'root' })
export class SeapService {
  private readonly http = inject(HttpClient);

  listTenders(filter: SeapTenderFilter = {}) {
    let params = new HttpParams();
    for (const categoryId of filter.categoryIds ?? []) params = params.append('categoryIds', categoryId);
    for (const prefix of filter.cpvPrefixes ?? []) params = params.append('cpvPrefixes', prefix);
    for (const county of filter.counties ?? []) params = params.append('counties', county);
    if (filter.radiusKm != null && filter.radiusKm > 0) params = params.set('radiusKm', String(filter.radiusKm));
    if (filter.county) params = params.set('county', filter.county);
    if (filter.search) params = params.set('search', 'true');
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    return this.http.get<SeapTender[]>('/api/v1/seap/tenders', { params });
  }

  getTender(id: string) { return this.http.get<SeapTender>(`/api/v1/seap/tenders/${id}`); }
  getTenderPricing(id: string) { return this.http.get<TenderPricing>(`/api/v1/seap/tenders/${id}/pricing`); }
  /** Starts an auto-bid generation. The optional <paramref name="progressRequestId"/> lets the page poll
   *  real per-stage progress (Generating → Finalizing → Completed/Failed) while the request is in flight. */
  startAutoBid(id: string, progressRequestId?: string) {
    return this.http.post<AutoBidResponse>(`/api/v1/seap/tenders/${id}/auto-bid`, { progressRequestId });
  }
  /** Real per-stage progress of a running auto-bid generation (polled while POST /auto-bid is in flight). */
  getAutoBidProgress(id: string, requestId: string) {
    return this.http.get<PipelineProgress>(`/api/v1/seap/tenders/${id}/auto-bid/progress/${requestId}`);
  }
  getBidStatus(id: string) { return this.http.get<BidStatus>(`/api/v1/seap/tenders/${id}/bid-status`); }

  /** Downloads one auto-generated proposal file (technical .docx or bill of quantities .xlsx) as a blob. */
  downloadBidFile(id: string, kind: 'TechnicalProposal' | 'BillOfQuantities') {
    return this.http.get(`/api/v1/seap/tenders/${id}/bid-file`, { params: { kind }, responseType: 'blob' });
  }

  /** Downloads one original authority file (Fișa de date, Caiet de sarcini, clarificări) as a blob. */
  downloadAttachment(id: string, attachmentId: string) {
    return this.http.get(`/api/v1/seap/tenders/${id}/attachments/${attachmentId}`, { responseType: 'blob' });
  }
  matchTenderAvailability() { return this.http.get<SeapTenderMatchAvailability>('/api/v1/seap/tender-match/availability'); }
  matchTenders(input: string, turns: SeapClarificationTurn[] = []) {
    return this.http.post<SeapTenderMatchResult>('/api/v1/seap/tender-match', { input, turns });
  }
  saveMatchProfile(companyId: string, cpvPrefixes: string[], counties: string[]) {
    return this.http.put<SeapTenderMatchProfile>('/api/v1/seap/tender-match/profile', { companyId, cpvPrefixes, counties });
  }
}
