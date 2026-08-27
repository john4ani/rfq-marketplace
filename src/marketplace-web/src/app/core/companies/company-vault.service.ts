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

/** Stage of a running auto-bidding pipeline, returned by GET specifications/progress/{requestId}. */
export interface PipelineProgress {
  stage: 'Extracting' | 'Generating' | 'Finalizing' | 'Completed' | 'Failed';
  message: string | null;
}

/** Friendly Romanian labels for each pipeline stage, shown while the pipeline request is in flight. */
export const PIPELINE_STAGE_LABELS: Record<PipelineProgress['stage'], string> = {
  Extracting: 'Etapa 1/3 — se extrag tabelele și textul din Caietul de Sarcini…',
  Generating: 'Etapa 2/3 — Claude redactează propunerea tehnică…',
  Finalizing: 'Etapa 3/3 — se generează documentele (.docx / .xlsx)…',
  Completed: 'Propunerea a fost generată.',
  Failed: 'Pipeline-ul a eșuat.'
};

@Injectable({ providedIn: 'root' })
export class CompanyVaultService {
  private readonly http = inject(HttpClient);
  list(companyId: string) { return this.http.get<VaultDocumentView[]>(`/api/v1/companies/${companyId}/vault`); }
  upload(companyId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<VaultDocumentView>(`/api/v1/companies/${companyId}/vault/documents`, form);
  }

  /** Caiet de sarcini: upload PDF → Azure Document Intelligence → Markdown → LLM proposal → .docx/.xlsx.
   *  <paramref name="progressRequestId"/> lets the caller poll GET specifications/progress/{id} for real
   *  per-stage progress while this request runs (30–60s for a 14–20 page Caiet). */
  uploadSpecification(companyId: string, file: File, progressRequestId?: string) {
    const form = new FormData();
    form.append('file', file);
    const params = progressRequestId ? { progressRequestId } : undefined;
    return this.http.post<SpecificationBidResult>(`/api/v1/companies/${companyId}/vault/specifications`, form, { params });
  }

  /** Re-runs the LLM proposal step for an already-extracted Caiet de sarcini. */
  generateFromSpecification(companyId: string, documentId: string, progressRequestId?: string) {
    const params = progressRequestId ? { progressRequestId } : undefined;
    return this.http.post<SpecificationBidResult>(`/api/v1/companies/${companyId}/vault/specifications/${documentId}/generate`, {}, { params });
  }

  /** Real per-stage progress of a running auto-bidding pipeline (polled while the upload/generate request
   *  is in flight so the UI can show "Etapa 1/3: se extrag tabelele…" instead of an endless spinner). */
  getSpecificationProgress(companyId: string, requestId: string) {
    return this.http.get<PipelineProgress>(`/api/v1/companies/${companyId}/vault/specifications/progress/${requestId}`);
  }

  /** Downloads one generated proposal file (technical .docx or bill of quantities .xlsx) as a blob. */
  downloadGeneratedFile(companyId: string, documentId: string, kind: 'TechnicalProposal' | 'BillOfQuantities') {
    return this.http.get(`/api/v1/companies/${companyId}/vault/documents/${documentId}/file`, {
      params: { kind },
      responseType: 'blob'
    });
  }
}
