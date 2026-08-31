import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { CompanyVaultService } from './company-vault.service';

const DISMISS_STORAGE_PREFIX = 'compaspot.vaultPromptDismissed.';

/**
 * Shared readiness check used before „Caută în SEAP" and „AI Auto-Bid": a tender can only be answered
 * well when the Document Repository (Company Vault) contains documents that were processed (extracted +
 * chunked) and can feed the RAG context. Also owns the „don't show this message again" preference so the
 * same prompt is never duplicated across the SEAP pages.
 */
@Injectable({ providedIn: 'root' })
export class VaultReadinessService {
  private readonly vault = inject(CompanyVaultService);

  /** True when at least one uploaded document is usable (extracted and chunked) for auto-bidding. */
  hasUsableDocuments(companyId: string): Observable<boolean> {
    return this.vault.list(companyId).pipe(
      map(documents => documents.some(document => document.extracted && document.chunkCount > 0))
    );
  }

  /** The user permanently dismissed the „no relevant documents yet" prompt for this company. */
  isPromptDismissed(companyId: string): boolean {
    try {
      return localStorage.getItem(DISMISS_STORAGE_PREFIX + companyId) === 'true';
    } catch {
      // Storage unavailable (private mode); fall back to showing the prompt.
      return false;
    }
  }

  /** Persists the permanent dismissal („Am făcut toate setările.") for this company. */
  dismissForever(companyId: string): void {
    try {
      localStorage.setItem(DISMISS_STORAGE_PREFIX + companyId, 'true');
    } catch {
      // Storage unavailable; the choice still applies for this session.
    }
  }
}
