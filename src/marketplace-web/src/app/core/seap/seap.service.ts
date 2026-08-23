import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface SeapTender {
  id: string;
  seapExternalId: string;
  title: string;
  description: string | null;
  cpvCodes: string[];
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

export interface BidStatus {
  status: SeapTender['status'];
  proposalTitle: string | null;
  technicalProposalUrl: string | null;
  billOfQuantitiesUrl: string | null;
}

export interface AutoBidResponse {
  ready: boolean;
  shadowMode: boolean;
  checkoutUrl?: string;
  sessionId?: string;
  priceEur?: number;
}

@Injectable({ providedIn: 'root' })
export class SeapService {
  private readonly http = inject(HttpClient);
  listTenders() { return this.http.get<SeapTender[]>('/api/v1/seap/tenders'); }
  getTender(id: string) { return this.http.get<SeapTender>(`/api/v1/seap/tenders/${id}`); }
  startAutoBid(id: string) { return this.http.post<AutoBidResponse>(`/api/v1/seap/tenders/${id}/auto-bid`, {}); }
  getBidStatus(id: string) { return this.http.get<BidStatus>(`/api/v1/seap/tenders/${id}/bid-status`); }
}
