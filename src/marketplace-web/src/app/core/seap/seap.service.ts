import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, of, switchMap, throwError } from 'rxjs';
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
  /** Numeric id of the notice in SEAP, when persisted — the source for resolving the published documents. */
  noticeId?: number | null;
  /** True when the AI summary was generated with the text of the published documents (not only metadata). */
  summaryGroundedInDocuments?: boolean;
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
  /** Transient note set by POST /summary when the synthesis fell back to notice metadata because the
   *  published documents (Fișa de date / Caiet de sarcini) could not be downloaded or read automatically. */
  summaryDocumentNote?: string | null;
}

/** Envelope returned by POST /api/v1/seap/tenders/resolve for BOTH HTTP 200 and 202: a ready/partial tender
 *  carries <c>tender</c> (full detail DTO); while the ingestion run processes <c>tender</c> is null and the
 *  SPA only needs <c>tenderId</c>/<c>runId</c>/<c>status</c>. */
export interface SeapResolveResponse {
  tenderId?: string;
  noticeNo?: string;
  runId?: string | null;
  status?: 'Pending' | 'FetchingDocuments' | 'Analyzing' | 'Ready' | 'Partial' | 'Failed';
  message?: string;
  tender?: SeapTender | null;
}

export interface SeapIngestionStatus {
  tenderId: string;
  noticeNo: string;
  status: 'Pending' | 'FetchingDocuments' | 'Analyzing' | 'Ready' | 'Partial' | 'Failed';
  importMode: string;
  runId: string | null;
  attemptCount: number;
  startedAt: string | null;
  completedAt: string | null;
  lastErrorCode: string | null;
  lastErrorMessage: string | null;
  documentSetHash: string | null;
  analysisVersion: number | null;
  /** Complete / Partial / None — how fully the published documents were content-verified. */
  digestStatus?: string | null;
  /** Stage checkpoints surfaced for progress UI (null until the checkpoint was persisted). */
  documentsFetchedAt?: string | null;
  textExtractedAt?: string | null;
}

/** Stage labels for the ingestion polling UI (Livrarea 3). */
export const INGESTION_STAGE_LABELS: Record<SeapIngestionStatus['status'], string> = {
  Pending: 'Anunțul a fost găsit. Pregătim importul…',
  FetchingDocuments: 'Descărcăm documentele publicate…',
  Analyzing: 'Analizăm cerințele anunțului…',
  Ready: 'Anunțul este gata.',
  Partial: 'Anunțul este disponibil parțial.',
  Failed: 'Procesarea anunțului a eșuat.'
};

export interface SeapTenderFilter {
  categoryIds?: string[];
  radiusKm?: number | null;
  county?: string | null;
  cpvPrefixes?: string[];
  counties?: string[];
  /** Restricts the feed to a single tender status — „ReadyForBidding" for the discovery tab („Caută
   *  anunțuri SEAP"), „BidGenerated" for the generated-offers history tab („Oferte Generate"). */
  status?: SeapTender['status'];
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
    if (filter.status) params = params.set('status', filter.status);
    if (filter.search) params = params.set('search', 'true');
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    return this.http.get<SeapTender[]>('/api/v1/seap/tenders', { params });
  }

  getTender(id: string) { return this.http.get<SeapTender>(`/api/v1/seap/tenders/${id}`); }
  /** Direct-hit resolve by SEAP notice number (SCN…/CN…): the backend answers with the unified envelope —
   *  HTTP 200 with a ready/partial <c>tender</c>, or HTTP 202 while the ingestion run processes (tender null;
   *  this method surfaces the friendly message until the full polling UI lands). */
  getTenderByExternalId(noticeNo: string): Observable<SeapTender> {
    return this.http.post<SeapResolveResponse>('/api/v1/seap/tenders/resolve', { noticeNo }).pipe(
      switchMap(envelope => {
        if (envelope?.tender) return of(envelope.tender);
        return throwError(() => ({
          error: {
            detail: envelope?.message ?? 'Anunțul a fost găsit, dar procesarea în fundal este încă în curs. Reîncearcă în scurt timp.'
          }
        }));
      })
    );
  }
  /** Direct-hit resolve exposing the full HTTP response (envelope + Location/Retry-After headers) so the SPA
   *  can navigate to the ingestion status URL and honor Retry-After while the run processes (Livrarea 3). */
  resolveNotice(noticeNo: string): Observable<HttpResponse<SeapResolveResponse>> {
    return this.http.post<SeapResolveResponse>('/api/v1/seap/tenders/resolve', { noticeNo }, { observe: 'response' });
  }
  /** Polls the persisted ingestion state of a tender (status + per-stage checkpoints). */
  getIngestionStatus(tenderId: string): Observable<SeapIngestionStatus> {
    return this.http.get<SeapIngestionStatus>(`/api/v1/seap/tenders/${tenderId}/ingestion-status`);
  }
  /** Resolves the published documents on demand for a tender that has none (idempotent server-side).
   *  Returns the same detail projection as <c>getTender</c>. */
  refreshDocuments(id: string) { return this.http.post<SeapTender>(`/api/v1/seap/tenders/${id}/documents`, {}); }
  /** Generates the AI summary on demand for a tender that has none (idempotent server-side: an existing
   *  summary is returned as-is). Returns the same detail projection as <c>getTender</c>. */
  generateSummary(id: string) { return this.http.post<SeapTender>(`/api/v1/seap/tenders/${id}/summary`, {}); }
  getTenderPricing(id: string) { return this.http.get<TenderPricing>(`/api/v1/seap/tenders/${id}/pricing`); }
  /** Starts an auto-bid generation. The <paramref name="companyId"/> identifies the company whose SEAP
   *  qualification vault feeds the proposal (validated server-side with CanRepresentCompany; a null value
   *  falls back to placeholders). The optional <paramref name="progressRequestId"/> lets the page poll
   *  real per-stage progress (Generating → Finalizing → Completed/Failed) while the request is in flight. */
  startAutoBid(id: string, companyId: string | null, progressRequestId?: string) {
    return this.http.post<AutoBidResponse>(`/api/v1/seap/tenders/${id}/auto-bid`, { companyId, progressRequestId });
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
