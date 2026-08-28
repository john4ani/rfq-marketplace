import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

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
  attachments: { id: string; fileName: string; s3Url: string }[];
}

export interface SeapTenderFilter {
  categoryIds?: string[];
  radiusKm?: number | null;
  county?: string | null;
  cpvPrefixes?: string[];
  counties?: string[];
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
    return this.http.get<SeapTender[]>('/api/v1/seap/tenders', { params });
  }

  getTender(id: string) { return this.http.get<SeapTender>(`/api/v1/seap/tenders/${id}`); }
  getTenderPricing(id: string) { return this.http.get<TenderPricing>(`/api/v1/seap/tenders/${id}/pricing`); }
  startAutoBid(id: string) { return this.http.post<AutoBidResponse>(`/api/v1/seap/tenders/${id}/auto-bid`, {}); }
  getBidStatus(id: string) { return this.http.get<BidStatus>(`/api/v1/seap/tenders/${id}/bid-status`); }
  matchTenderAvailability() { return this.http.get<SeapTenderMatchAvailability>('/api/v1/seap/tender-match/availability'); }
  matchTenders(input: string, turns: SeapClarificationTurn[] = []) {
    return this.http.post<SeapTenderMatchResult>('/api/v1/seap/tender-match', { input, turns });
  }
  saveMatchProfile(companyId: string, cpvPrefixes: string[], counties: string[]) {
    return this.http.put<SeapTenderMatchProfile>('/api/v1/seap/tender-match/profile', { companyId, cpvPrefixes, counties });
  }
}
