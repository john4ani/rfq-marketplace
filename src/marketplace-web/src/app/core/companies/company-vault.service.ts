import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface VaultDocumentView {
  id: string;
  fileName: string;
  documentType: string;
  extracted: boolean;
  pageCount: number | null;
  chunkCount: number;
  uploadedAt: string;
}

export interface SpecificationBidResult {
  documentId: string;
  fileName: string;
  documentUrl: string;
  markdown: string;
  proposalTitle: string;
  technicalProposalUrl: string;
  billOfQuantitiesUrl: string;
  shadowMode: boolean;
  generatedAt: string;
}

@Injectable({ providedIn: 'root' })
export class CompanyVaultService {
  private readonly http = inject(HttpClient);
  list(companyId: string) { return this.http.get<VaultDocumentView[]>(`/api/v1/companies/${companyId}/vault`); }
  upload(companyId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<VaultDocumentView>(`/api/v1/companies/${companyId}/vault/documents`, form);
  }

  /** Caiet de sarcini: upload PDF → Azure Document Intelligence → Markdown → LLM proposal → .docx/.xlsx. */
  uploadSpecification(companyId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<SpecificationBidResult>(`/api/v1/companies/${companyId}/vault/specifications`, form);
  }

  /** Re-runs the LLM proposal step for an already-extracted Caiet de sarcini. */
  generateFromSpecification(companyId: string, documentId: string) {
    return this.http.post<SpecificationBidResult>(`/api/v1/companies/${companyId}/vault/specifications/${documentId}/generate`, {});
  }

  /** Downloads one generated proposal file (technical .docx or bill of quantities .xlsx) as a blob. */
  downloadGeneratedFile(companyId: string, documentId: string, kind: 'TechnicalProposal' | 'BillOfQuantities') {
    return this.http.get(`/api/v1/companies/${companyId}/vault/documents/${documentId}/file`, {
      params: { kind },
      responseType: 'blob'
    });
  }
}
