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

@Injectable({ providedIn: 'root' })
export class CompanyVaultService {
  private readonly http = inject(HttpClient);
  list(companyId: string) { return this.http.get<VaultDocumentView[]>(`/api/v1/companies/${companyId}/vault`); }
  upload(companyId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<VaultDocumentView>(`/api/v1/companies/${companyId}/vault/documents`, form);
  }
}
